using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// Every case here decides whether a second message gets sent and billed, which is why
/// they are unit tests rather than something anyone re-checks by hand against a provider.
/// </summary>
public class FallbackHandlerServiceTests
{
    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly Guid PrimaryConfigId = Guid.NewGuid();
    private static readonly Guid BackupConfigId = Guid.NewGuid();

    private static ChannelConfig Backup() => new()
    {
        Id = BackupConfigId,
        ChannelType = ChannelType.WP,
        Label = "Backup WhatsApp",
        LabelSlug = "backupwhatsapp",
        IsActive = true,
        ConfigJson = "{}",
    };

    private static DispatchJob FailedPrimary() => new()
    {
        Id = Guid.NewGuid(),
        MessageRequestId = RequestId,
        ChannelType = ChannelType.WP,
        ChannelConfigId = PrimaryConfigId,
        MessageContent = "hi",
        ToNumber = "8801700000001",
        Status = JobStatus.Failed,
        IsFallback = false,
    };

    private static FallbackPolicy SameTypePolicy() => new()
    {
        Name = "wp-to-wp",
        TriggerChannel = ChannelType.WP,
        FallbackChannel = ChannelType.WP,
        TriggerChannelConfigId = PrimaryConfigId,
        FallbackChannelConfigId = BackupConfigId,
        IsActive = true,
        // Hotfix batch 9, Task 9: IsGlobal=false is what a normal, channel-scoped policy
        // actually looks like in the live DB — the admin UI's "Apply this rule to all
        // channels" toggle defaulted off, so every test in this file used to only prove
        // the matching logic worked for a combination (scoped + IsGlobal=true) nothing
        // real ever produced. False here, matching reality, is the regression guard.
        IsGlobal = false,
    };

    private static (FallbackHandlerService Service, FakeJobRepositoryWithExisting Jobs,
        FakeDispatchJobRecipientRepository Recipients) Build(
        MessageRequest request, params FallbackPolicy[] policies)
    {
        var jobs = new FakeJobRepositoryWithExisting([FailedPrimary()]);
        var recipients = new FakeDispatchJobRecipientRepository();

        var service = new FallbackHandlerService(
            jobs,
            recipients,
            new FakeMessageRequestRepository(request),
            new FakeFallbackPolicyRepository(policies),
            new FakeChannelConfigRepository(Backup()),
            new PlainTextEncryption(),
            new FakeSenderFactory(new CapturingSender(ChannelType.WP)),
            NullLogger<FallbackHandlerService>.Instance);

        return (service, jobs, recipients);
    }

    private static MessageRequest Request(bool isMultiChannel) => new()
    {
        Id = RequestId,
        ToUser = "8801700000001",
        IsMultiChannel = isMultiChannel,
    };

    [Fact]
    public async Task A_channel_scoped_policy_fires_even_when_not_flagged_IsGlobal()
    {
        // The actual hotfix batch 9, Task 9 bug: GetActiveGlobalPoliciesAsync used to also
        // require IsGlobal==true, a flag the admin UI's "Apply this rule to all channels"
        // toggle defaults OFF for every new policy — so a policy an admin had correctly
        // scoped to one failing channel, exactly as the Trigger Channel picker above that
        // toggle already lets them do, was silently never found. Proved live against the
        // real stack before this fix: an active, correctly-scoped policy produced
        // "[FALLBACK] No active policy covers..." and zero fallback jobs.
        var policy = SameTypePolicy();
        Assert.False(policy.IsGlobal);

        var (service, jobs, _) = Build(Request(isMultiChannel: false), policy);

        await service.HandleAsync(RequestId);

        var fallback = Assert.Single(jobs.Added);
        Assert.True(fallback.IsFallback);
        Assert.Equal(BackupConfigId, fallback.ChannelConfigId);
    }

    [Fact]
    public async Task A_single_channel_send_still_gets_its_fallback()
    {
        var (service, jobs, recipients) = Build(Request(isMultiChannel: false), SameTypePolicy());

        await service.HandleAsync(RequestId);

        var fallback = Assert.Single(jobs.Added);
        Assert.True(fallback.IsFallback);
        Assert.Equal(BackupConfigId, fallback.ChannelConfigId);

        // Hotfix batch 3: a recipient rescued by a fallback job reads Fallback, not
        // Pending — the per-recipient mirror of MessageStatus.Fallback at the request
        // level, visible immediately rather than waiting on a status-sync run.
        var recipient = Assert.Single(recipients.Added);
        Assert.Equal(DeliveryStatus.Fallback, recipient.DeliveryStatus);
    }

    [Fact]
    public async Task A_multi_channel_send_gets_no_fallback_at_all()
    {
        // The sender asked for two deliveries, so a third would be charged for a message
        // they never requested.
        var (service, jobs, _) = Build(Request(isMultiChannel: true), SameTypePolicy());

        await service.HandleAsync(RequestId);

        Assert.Empty(jobs.Added);
    }

    [Fact]
    public async Task A_cross_type_policy_without_the_opt_in_is_skipped()
    {
        var crossType = SameTypePolicy();
        crossType.FallbackChannel = ChannelType.SMS;
        crossType.FallbackChannelConfigId = null;
        crossType.AllowCrossChannel = false;

        var (service, jobs, _) = Build(Request(isMultiChannel: false), crossType);

        await service.HandleAsync(RequestId);

        // Enforced at dispatch as well as at save time, so a row edited straight in the
        // database cannot quietly start billing a different channel.
        Assert.Empty(jobs.Added);
    }

    [Fact]
    public async Task A_policy_pointing_back_at_the_channel_that_just_failed_is_skipped()
    {
        var selfReferential = SameTypePolicy();
        selfReferential.FallbackChannelConfigId = PrimaryConfigId;

        var jobs = new FakeJobRepositoryWithExisting([FailedPrimary()]);
        var primary = Backup();
        primary.Id = PrimaryConfigId;

        var service = new FallbackHandlerService(
            jobs,
            new FakeDispatchJobRecipientRepository(),
            new FakeMessageRequestRepository(Request(isMultiChannel: false)),
            new FakeFallbackPolicyRepository(selfReferential),
            new FakeChannelConfigRepository(primary),
            new PlainTextEncryption(),
            new FakeSenderFactory(new CapturingSender(ChannelType.WP)),
            NullLogger<FallbackHandlerService>.Instance);

        await service.HandleAsync(RequestId);

        Assert.Empty(jobs.Added);
    }

    [Fact]
    public async Task A_policy_scoped_to_the_failed_channel_wins_over_a_catch_all_for_its_type()
    {
        // Listed first on purpose: the choice used to fall to whichever row came back first,
        // so a catch-all policy added later could override a rule written for this channel.
        var catchAll = SameTypePolicy();
        catchAll.Name = "catch-all wp-to-sms";
        catchAll.TriggerChannelConfigId = null;
        catchAll.FallbackChannel = ChannelType.SMS;
        catchAll.FallbackChannelConfigId = null;
        catchAll.AllowCrossChannel = true;

        var (service, jobs, _) = Build(Request(isMultiChannel: false), catchAll, SameTypePolicy());

        await service.HandleAsync(RequestId);

        var fallback = Assert.Single(jobs.Added);
        Assert.Equal(ChannelType.WP, fallback.ChannelType);
    }

    [Fact]
    public async Task A_catch_all_policy_still_covers_a_channel_with_no_policy_of_its_own()
    {
        var catchAll = SameTypePolicy();
        catchAll.TriggerChannelConfigId = null;

        var (service, jobs, _) = Build(Request(isMultiChannel: false), catchAll);

        await service.HandleAsync(RequestId);

        Assert.Single(jobs.Added);
    }

    [Fact]
    public async Task A_policy_scoped_to_another_channel_does_not_cover_this_failure()
    {
        var elsewhere = SameTypePolicy();
        elsewhere.TriggerChannelConfigId = Guid.NewGuid();

        var (service, jobs, _) = Build(Request(isMultiChannel: false), elsewhere);

        await service.HandleAsync(RequestId);

        Assert.Empty(jobs.Added);
    }

    [Fact]
    public async Task An_inactive_fallback_channel_is_skipped_not_used()
    {
        var inactive = Backup();
        inactive.IsActive = false;

        var jobs = new FakeJobRepositoryWithExisting([FailedPrimary()]);
        var service = new FallbackHandlerService(
            jobs,
            new FakeDispatchJobRecipientRepository(),
            new FakeMessageRequestRepository(Request(isMultiChannel: false)),
            new FakeFallbackPolicyRepository(SameTypePolicy()),
            new FakeChannelConfigRepository(inactive),
            new PlainTextEncryption(),
            new FakeSenderFactory(new CapturingSender(ChannelType.WP)),
            NullLogger<FallbackHandlerService>.Instance);

        await service.HandleAsync(RequestId);

        Assert.Empty(jobs.Added);
    }

    [Fact]
    public async Task Nothing_happens_when_no_primary_job_failed()
    {
        var jobs = new FakeJobRepositoryWithExisting([]);

        var service = new FallbackHandlerService(
            jobs,
            new FakeDispatchJobRecipientRepository(),
            new FakeMessageRequestRepository(Request(isMultiChannel: false)),
            new FakeFallbackPolicyRepository(SameTypePolicy()),
            new FakeChannelConfigRepository(Backup()),
            new PlainTextEncryption(),
            new FakeSenderFactory(new CapturingSender(ChannelType.WP)),
            NullLogger<FallbackHandlerService>.Instance);

        await service.HandleAsync(RequestId);

        Assert.Empty(jobs.Added);
    }
}
