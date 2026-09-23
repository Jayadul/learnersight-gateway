using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// Resend is recipient-level because SMS and Voice batch many numbers into one job:
/// retrying the job would send again to everyone it already reached. These tests pin
/// that, the same-type restriction, and — since hotfix batch 4, task 2a — that a
/// recipient retried more than once still shows as exactly one row, updated in place,
/// with each attempt's own outcome kept in ResendAttempt history rather than a new
/// dispatch_job_recipients row per try.
/// </summary>
public class ResendServiceTests
{
    private static readonly Guid OriginalChannelId = Guid.NewGuid();
    private static readonly Guid OtherSmsChannelId = Guid.NewGuid();
    private static readonly Guid WhatsAppChannelId = Guid.NewGuid();

    private static ChannelConfig Channel(
        Guid id, ChannelType type, bool isActive = true, string label = "Channel") => new()
    {
        Id = id,
        ChannelType = type,
        Provider = type == ChannelType.SMS ? Provider.Reve : Provider.WaTrend,
        ProviderName = "test",
        Label = label,
        IsActive = isActive,
        ConfigJson = "{}"
    };

    private sealed class Repo(params ChannelConfig[] configs) : IChannelConfigRepository
    {
        public Task<ChannelConfig?> GetByIdAsync(Guid id)
            => Task.FromResult(configs.FirstOrDefault(c => c.Id == id));
        public Task<List<ChannelConfig>> GetAllAsync() => Task.FromResult(configs.ToList());
        public void Update(ChannelConfig entity) { }
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
        public Task<ChannelConfig?> GetDefaultByChannelTypeAsync(ChannelType t) => Unused.Fail<Task<ChannelConfig?>>();
        public Task<ChannelConfig?> GetActiveBySlugAsync(ChannelType t, string s) => Unused.Fail<Task<ChannelConfig?>>();
        public Task<List<ChannelConfig>> GetAllByChannelTypeAsync(ChannelType t) => Unused.Fail<Task<List<ChannelConfig>>>();
        public Task SetDefaultAsync(Guid id, ChannelType t) => Unused.Fail<Task>();
        public Task<bool> SlugExistsAsync(ChannelType t, string s, Guid? e) => Unused.Fail<Task<bool>>();
        public Task AddAsync(ChannelConfig e) => Unused.Fail<Task>();
        public Task<List<ChannelConfig>> FindAsync(Expression<Func<ChannelConfig, bool>> p) => Unused.Fail<Task<List<ChannelConfig>>>();
        public void Delete(ChannelConfig e) => Unused.Fail<bool>();
    }

    private sealed class JobRepo(DispatchJob original) : IDispatchJobRepository
    {
        public List<DispatchJob> Added { get; } = [];
        public Task<DispatchJob?> GetByIdAsync(Guid id)
            => Task.FromResult(id == original.Id ? original : Added.FirstOrDefault(j => j.Id == id));
        public Task AddAsync(DispatchJob e) { Added.Add(e); return Task.CompletedTask; }
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
        public void Update(DispatchJob e) { }
        public Task<List<DispatchJob>> GetByMessageRequestIdAsync(Guid id) => Unused.Fail<Task<List<DispatchJob>>>();
        public Task<List<DispatchJob>> GetFailedWithChannelAsync() => Unused.Fail<Task<List<DispatchJob>>>();
        public Task<List<DispatchJob>> GetAllAsync() => Unused.Fail<Task<List<DispatchJob>>>();
        public Task<List<DispatchJob>> FindAsync(Expression<Func<DispatchJob, bool>> p) => Unused.Fail<Task<List<DispatchJob>>>();
        public void Delete(DispatchJob e) => Unused.Fail<bool>();
    }

    private sealed class RecipientRepo(DispatchJobRecipient original) : IDispatchJobRecipientRepository
    {
        public List<DispatchJobRecipient> Added { get; } = [];
        public int UpdateCount { get; private set; }
        public Task<DispatchJobRecipient?> GetByIdAsync(Guid id)
            => Task.FromResult(id == original.Id ? original : Added.FirstOrDefault(r => r.Id == id));
        public Task AddAsync(DispatchJobRecipient e) { Added.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<DispatchJobRecipient> r) { Added.AddRange(r); return Task.CompletedTask; }
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
        public void Update(DispatchJobRecipient e) { UpdateCount++; }
        public Task<List<DispatchJobRecipient>> GetByDispatchJobIdAsync(Guid id) => Unused.Fail<Task<List<DispatchJobRecipient>>>();
        public Task<List<DispatchJobRecipient>> GetPendingForChannelAsync(
            Guid c, DateTimeOffset f, DateTimeOffset t, int l) => Unused.Fail<Task<List<DispatchJobRecipient>>>();
        public Task<(List<DispatchJobRecipient> Items, int Total)> GetPagedAsync(
            int page, int size, DateTimeOffset? from, DateTimeOffset? to,
            ChannelType? channelType, Guid? channelConfigId,
            DeliveryStatus? deliveryStatus, string? recipient, string? failureReason = null)
            => Unused.Fail<Task<(List<DispatchJobRecipient>, int)>>();
        public Task<List<DispatchJobRecipient>> GetAllAsync() => Unused.Fail<Task<List<DispatchJobRecipient>>>();
        public Task<List<DispatchJobRecipient>> FindAsync(Expression<Func<DispatchJobRecipient, bool>> p) => Unused.Fail<Task<List<DispatchJobRecipient>>>();
        public void Delete(DispatchJobRecipient e) => Unused.Fail<bool>();
    }

    private sealed class AttemptRepo : IResendAttemptRepository
    {
        public List<ResendAttempt> Added { get; } = [];
        public Task AddAsync(ResendAttempt e) { Added.Add(e); return Task.CompletedTask; }
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
        public Task<List<ResendAttempt>> GetByRecipientIdAsync(Guid id)
            => Task.FromResult(Added.Where(a => a.DispatchJobRecipientId == id).ToList());
        public Task<ResendAttempt?> GetByIdAsync(Guid id) => Unused.Fail<Task<ResendAttempt?>>();
        public Task<List<ResendAttempt>> GetAllAsync() => Unused.Fail<Task<List<ResendAttempt>>>();
        public Task<List<ResendAttempt>> FindAsync(Expression<Func<ResendAttempt, bool>> p) => Unused.Fail<Task<List<ResendAttempt>>>();
        public void Update(ResendAttempt e) => Unused.Fail<bool>();
        public void Delete(ResendAttempt e) => Unused.Fail<bool>();
    }

    private static (ResendService Service, JobRepo Jobs, RecipientRepo Recipients,
        AttemptRepo Attempts, DispatchJobRecipient Original) Build(
        JobStatus resendOutcome = JobStatus.Sent, params ChannelConfig[] channels)
    {
        var originalJob = new DispatchJob
        {
            Id = Guid.NewGuid(),
            MessageRequestId = Guid.NewGuid(),
            ChannelType = ChannelType.SMS,
            ChannelConfigId = OriginalChannelId,
            MessageContent = "hello",
            // The original batched three numbers; only one of them failed.
            ToNumber = "8801748848039,8801969155575,8801700000003",
            Status = JobStatus.Failed
        };

        var original = new DispatchJobRecipient
        {
            Id = Guid.NewGuid(),
            DispatchJobId = originalJob.Id,
            Recipient = "8801748848039",
            DeliveryStatus = DeliveryStatus.Failed
        };

        var jobs = new JobRepo(originalJob);
        var recipients = new RecipientRepo(original);
        var attempts = new AttemptRepo();

        var all = channels.Length > 0
            ? channels
            : [Channel(OriginalChannelId, ChannelType.SMS, label: "SMS Default")];

        var service = new ResendService(
            jobs, recipients, attempts, new Repo(all), new PlainTextEncryption(),
            new FakeSenderFactory(new OutcomeSender(resendOutcome)),
            NullLogger<ResendService>.Instance);

        return (service, jobs, recipients, attempts, original);
    }

    private sealed class OutcomeSender(JobStatus outcome) : Application.Strategies.IChannelSender
    {
        public ChannelType ChannelType => ChannelType.SMS;
        public Provider Provider => Provider.Reve;
        public Task SendAsync(
            DispatchJob job, Dictionary<string, string> config, CancellationToken ct = default)
        {
            job.Status = outcome;
            job.ProviderMessageId = outcome == JobStatus.Sent ? "RETRY-1" : null;
            job.FailureReason = outcome == JobStatus.Failed ? "still failing" : null;
            job.ProviderRawResponse = outcome == JobStatus.Sent ? """{"ok":true}""" : """{"ok":false}""";
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_resend_sends_only_the_selected_recipient_not_the_whole_batch()
    {
        var (service, jobs, _, _, original) = Build();

        await service.ResendAsync(new ResendRequest { RecipientIds = [original.Id] });

        // The original job carried three numbers; the retry carries exactly one.
        var retry = Assert.Single(jobs.Added);
        Assert.Equal("8801748848039", retry.ToNumber);
    }

    [Fact]
    public async Task A_successful_resend_updates_the_same_row_in_place_rather_than_adding_one()
    {
        var (service, _, recipients, attempts, original) = Build();

        var result = await service.ResendAsync(
            new ResendRequest { RecipientIds = [original.Id] });

        // No new dispatch_job_recipients row — the same row is updated.
        Assert.Empty(recipients.Added);
        Assert.Equal(1, recipients.UpdateCount);
        Assert.Equal(1, result.Resent);

        // The row itself now reflects the successful retry, not the original failure —
        // a resend that succeeds must stop looking like a failure.
        Assert.Equal(DeliveryStatus.Pending, original.DeliveryStatus);
        Assert.Equal("RETRY-1", original.ProviderReferenceId);

        // But the attempt is recorded, so what happened is not lost.
        var attempt = Assert.Single(attempts.Added);
        Assert.Equal(original.Id, attempt.DispatchJobRecipientId);
        Assert.Equal(DeliveryStatus.Pending, attempt.DeliveryStatus);
    }

    [Fact]
    public async Task A_retry_that_fails_again_updates_the_row_in_place_and_is_recorded()
    {
        var (service, _, recipients, attempts, original) = Build(resendOutcome: JobStatus.Failed);

        var result = await service.ResendAsync(
            new ResendRequest { RecipientIds = [original.Id] });

        Assert.Equal(0, result.Resent);
        Assert.Equal(1, result.Failed);
        Assert.Empty(recipients.Added);
        Assert.Equal(DeliveryStatus.Failed, original.DeliveryStatus);
        Assert.Equal("still failing", result.Outcomes[0].Reason);
        Assert.Single(attempts.Added);
    }

    [Fact]
    public async Task Three_attempts_on_one_recipient_leave_one_row_and_three_attempt_records()
    {
        // The exact scenario the client complained about: retried three times must not
        // read as three different sends.
        var (service, jobs, recipients, attempts, original) =
            Build(resendOutcome: JobStatus.Failed);

        await service.ResendAsync(new ResendRequest { RecipientIds = [original.Id] });
        await service.ResendAsync(new ResendRequest { RecipientIds = [original.Id] });
        await service.ResendAsync(new ResendRequest { RecipientIds = [original.Id] });

        Assert.Equal(3, jobs.Added.Count);
        Assert.Empty(recipients.Added);
        Assert.Equal(3, recipients.UpdateCount);
        Assert.Equal(3, attempts.Added.Count);
        // The row always points at the most recent attempt's job.
        Assert.Equal(jobs.Added[^1].Id, original.DispatchJobId);
    }

    [Fact]
    public async Task Resending_through_another_channel_of_the_same_type_is_allowed()
    {
        var (service, jobs, _, _, original) = Build(
            JobStatus.Sent,
            Channel(OriginalChannelId, ChannelType.SMS, label: "SMS Default"),
            Channel(OtherSmsChannelId, ChannelType.SMS, label: "SMS Backup"));

        await service.ResendAsync(new ResendRequest
        {
            RecipientIds = [original.Id],
            ChannelConfigId = OtherSmsChannelId
        });

        Assert.Equal(OtherSmsChannelId, Assert.Single(jobs.Added).ChannelConfigId);
    }

    [Fact]
    public async Task Resending_across_channel_types_is_refused()
    {
        // An SMS-only contact has no guaranteed WhatsApp number, and the recipient
        // format assumptions differ per channel. Same rule fallback uses.
        var (service, _, _, _, original) = Build(
            JobStatus.Sent,
            Channel(OriginalChannelId, ChannelType.SMS, label: "SMS Default"),
            Channel(WhatsAppChannelId, ChannelType.WP, label: "WhatsApp"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResendAsync(new ResendRequest
            {
                RecipientIds = [original.Id],
                ChannelConfigId = WhatsAppChannelId
            }));

        Assert.Contains("cannot be resent on a WP channel", ex.Message);
    }

    [Fact]
    public async Task Resending_on_a_deactivated_original_channel_is_skipped_with_a_reason()
    {
        var (service, jobs, _, _, original) = Build(
            JobStatus.Sent,
            Channel(OriginalChannelId, ChannelType.SMS, isActive: false, label: "Retired SMS"));

        var result = await service.ResendAsync(
            new ResendRequest { RecipientIds = [original.Id] });

        Assert.Empty(jobs.Added);
        Assert.Equal(1, result.Skipped);
        Assert.Contains("no longer active", result.Outcomes[0].Reason);
    }

    [Fact]
    public async Task The_same_recipient_selected_twice_is_only_sent_once()
    {
        // Otherwise a double-click bills two sends to one number.
        var (service, jobs, _, _, original) = Build();

        var result = await service.ResendAsync(
            new ResendRequest { RecipientIds = [original.Id, original.Id] });

        Assert.Single(jobs.Added);
        Assert.Equal(1, result.Resent);
    }

    [Fact]
    public async Task An_empty_selection_is_refused()
    {
        var (service, _, _, _, _) = Build();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResendAsync(new ResendRequest { RecipientIds = [] }));
    }
}
