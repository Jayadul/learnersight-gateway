using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// SMS and Voice batch every recipient into one job, so a job's status alone cannot say
/// which numbers arrived. These rows are where that detail lands, and they are the schema
/// the status sync writes into later — so what gets written at dispatch time is pinned
/// here rather than discovered once a sync is built on top of it.
/// </summary>
public class DispatchJobRecipientTests
{
    private static (ChannelDispatchService Service, FakeDispatchJobRecipientRepository Recipients)
        Build(ChannelType channelType, JobStatus outcome = JobStatus.Sent,
              string? providerMessageId = null)
    {
        var config = new ChannelConfig
        {
            ChannelType = channelType,
            Provider = channelType == ChannelType.Voice ? Provider.NetZeroVoice
                     : channelType == ChannelType.WP ? Provider.WaTrend
                     : Provider.Reve,
            ProviderName = "test",
            Label = "Test",
            IsActive = true,
            // PlainTextEncryption is identity, so this is stored readable on purpose.
            ConfigJson = """{"owner":"o","password":"p","authUserId":"a","apikey":"k","secretkey":"s","instance_id":"i","access_token":"t"}"""
        };

        var recipients = new FakeDispatchJobRecipientRepository();
        var service = new ChannelDispatchService(
            new FakeMessageRequestRepository(),
            new FakeDispatchJobRepository(),
            recipients,
            new FakeChannelConfigRepository(config),
            new PlainTextEncryption(),
            new FakeSenderFactory(new ResultSender(channelType, outcome, providerMessageId)),
            new NoOpFallbackHandler(),
            NullLogger<ChannelDispatchService>.Instance);

        return (service, recipients);
    }

    /// <summary>A sender that reports a fixed outcome, so the rows can be asserted.</summary>
    private sealed class ResultSender(
        ChannelType channelType, JobStatus outcome, string? providerMessageId)
        : Application.Strategies.IChannelSender
    {
        public ChannelType ChannelType { get; } = channelType;
        public Provider Provider => Provider.Reve;

        public Task SendAsync(
            DispatchJob job, Dictionary<string, string> config, CancellationToken ct = default)
        {
            job.Status = outcome;
            job.ProviderMessageId = providerMessageId;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_batched_sms_job_gets_one_row_per_recipient()
    {
        var (service, recipients) = Build(ChannelType.SMS);

        await service.DispatchAsync(Guid.NewGuid(), "8801700000001,8801700000002,8801700000003",
            [new ParsedChannelJob { ChannelType = ChannelType.SMS, Message = "hi" }]);

        Assert.Equal(3, recipients.Added.Count);
        Assert.Equal(
            ["8801700000001", "8801700000002", "8801700000003"],
            recipients.Added.Select(r => r.Recipient));
    }

    [Fact]
    public async Task Every_recipient_of_a_reve_batch_carries_the_same_reference_id()
    {
        // REVE returns one id for the whole batch. Recording it against each recipient is
        // deliberate: the status lookup takes that id, and all the rows resolve together.
        var (service, recipients) = Build(ChannelType.SMS, providerMessageId: "REVE-1");

        await service.DispatchAsync(Guid.NewGuid(), "8801700000001,8801700000002",
            [new ParsedChannelJob { ChannelType = ChannelType.SMS, Message = "hi" }]);

        Assert.All(recipients.Added, r => Assert.Equal("REVE-1", r.ProviderReferenceId));
    }

    [Fact]
    public async Task A_per_recipient_reference_beats_the_job_level_one_when_the_provider_gives_it()
    {
        // GenNet and Chithi return one id per recipient; those must land on their own
        // rows rather than every row sharing the job's id as REVE's legitimately do.
        var config = new ChannelConfig
        {
            ChannelType = ChannelType.SMS,
            Provider = Provider.GenNet,
            ProviderName = "test",
            Label = "GenNet",
            IsActive = true,
            ConfigJson = """{"api_token":"t","sid":"s"}"""
        };

        var recipients = new FakeDispatchJobRecipientRepository();
        var service = new ChannelDispatchService(
            new FakeMessageRequestRepository(),
            new FakeDispatchJobRepository(),
            recipients,
            new FakeChannelConfigRepository(config),
            new PlainTextEncryption(),
            new FakeSenderFactory(new PerRecipientSender()),
            new NoOpFallbackHandler(),
            NullLogger<ChannelDispatchService>.Instance);

        await service.DispatchAsync(Guid.NewGuid(), "8801700000001,8801700000002",
            [new ParsedChannelJob { ChannelType = ChannelType.SMS, Message = "hi" }]);

        Assert.Equal(
            ["REF-8801700000001", "REF-8801700000002"],
            recipients.Added.Select(r => r.ProviderReferenceId));
    }

    /// <summary>Reports a distinct provider id for every recipient, as GenNet does.</summary>
    private sealed class PerRecipientSender : Application.Strategies.IChannelSender
    {
        public ChannelType ChannelType => ChannelType.SMS;
        public Provider Provider => Provider.GenNet;

        public Task SendAsync(
            DispatchJob job, Dictionary<string, string> config, CancellationToken ct = default)
        {
            job.Status = JobStatus.Sent;
            job.ProviderMessageId = "BATCH-ID";
            job.RecipientReferences = job.ToNumber
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .ToDictionary(n => n.Trim(), n => $"REF-{n.Trim()}");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task An_accepted_send_leaves_recipients_pending_for_the_sync_to_resolve()
    {
        var (service, recipients) = Build(ChannelType.SMS);

        await service.DispatchAsync(Guid.NewGuid(), "8801700000001",
            [new ParsedChannelJob { ChannelType = ChannelType.SMS, Message = "hi" }]);

        Assert.Equal(DeliveryStatus.Pending, Assert.Single(recipients.Added).DeliveryStatus);
    }

    [Fact]
    public async Task A_failed_send_marks_recipients_failed_rather_than_awaiting_a_status()
    {
        // The message never reached the provider, so no status will ever arrive for it.
        // Leaving these Pending would have the sync chase them forever.
        var (service, recipients) = Build(ChannelType.SMS, outcome: JobStatus.Failed);

        await service.DispatchAsync(Guid.NewGuid(), "8801700000001",
            [new ParsedChannelJob { ChannelType = ChannelType.SMS, Message = "hi" }]);

        Assert.Equal(DeliveryStatus.Failed, Assert.Single(recipients.Added).DeliveryStatus);
    }

    [Fact]
    public async Task Whatsapp_dispatches_per_recipient_and_still_gets_one_row_each()
    {
        var (service, recipients) = Build(ChannelType.WP);

        await service.DispatchAsync(Guid.NewGuid(), "8801700000001,8801700000002",
            [new ParsedChannelJob { ChannelType = ChannelType.WP, Message = "hi" }]);

        Assert.Equal(2, recipients.Added.Count);
        // One job per number, so each row belongs to a different job.
        Assert.Equal(2, recipients.Added.Select(r => r.DispatchJobId).Distinct().Count());
    }
}
