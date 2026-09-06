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
        IsGlobal = true,
    };

    private static (FallbackHandlerService Service, FakeJobRepositoryWithExisting Jobs) Build(
        MessageRequest request, params FallbackPolicy[] policies)
    {
        var jobs = new FakeJobRepositoryWithExisting([FailedPrimary()]);

        var service = new FallbackHandlerService(
            jobs,
            new FakeDispatchJobRecipientRepository(),
            new FakeMessageRequestRepository(request),
            new FakeFallbackPolicyRepository(policies),
            new FakeChannelConfigRepository(Backup()),
            new PlainTextEncryption(),
            new FakeSenderFactory(new CapturingSender(ChannelType.WP)),
            NullLogger<FallbackHandlerService>.Instance);

        return (service, jobs);
    }

    private static MessageRequest Request(bool isMultiChannel) => new()
    {
        Id = RequestId,
        ToUser = "8801700000001",
        IsMultiChannel = isMultiChannel,
    };

    [Fact]
    public async Task A_single_channel_send_still_gets_its_fallback()
    {
        var (service, jobs) = Build(Request(isMultiChannel: false), SameTypePolicy());

        await service.HandleAsync(RequestId);

        var fallback = Assert.Single(jobs.Added);
        Assert.True(fallback.IsFallback);
        Assert.Equal(BackupConfigId, fallback.ChannelConfigId);
    }

    [Fact]
    public async Task A_multi_channel_send_gets_no_fallback_at_all()
    {
        // The sender asked for two deliveries, so a third would be charged for a message
        // they never requested.
        var (service, jobs) = Build(Request(isMultiChannel: true), SameTypePolicy());

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

        var (service, jobs) = Build(Request(isMultiChannel: false), crossType);

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
