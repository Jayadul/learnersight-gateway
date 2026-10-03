using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// Hotfix batch 9, Task 10: a <c>template</c> /sendtext tag must reach the vendor the same
/// way Manual Send's template send already does — via IWhatsAppTemplateSender directly,
/// never IChannelSenderFactory (which has no slot for "template vs freeform text").
/// </summary>
public class ChannelDispatchServiceTemplateTests
{
    private sealed class StubTemplateSender(WhatsAppTemplateSendResult result) : IWhatsAppTemplateSender
    {
        public string? LastTemplateName { get; private set; }
        public List<string>? LastVariables { get; private set; }
        public string? LastToNumber { get; private set; }

        public Task<WhatsAppTemplateSendResult> SendAsync(
            string templateToken, string toNumber, string templateName,
            List<string> exampleArr, string? mediaUrl, string? baseUrl,
            CancellationToken ct = default)
        {
            LastToNumber = toNumber;
            LastTemplateName = templateName;
            LastVariables = exampleArr;
            return Task.FromResult(result);
        }
    }

    /// <summary>A sender that would fail the test outright if DispatchAsync ever called it for a template job.</summary>
    private sealed class UnreachableSender : IChannelSender
    {
        public ChannelType ChannelType => ChannelType.WP;
        public Provider Provider => Provider.WaTrend;
        public Task SendAsync(DispatchJob job, Dictionary<string, string> config, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "IChannelSenderFactory must never be used for a template job.");
    }

    private static ChannelConfig TemplateChannel() => new()
    {
        Id = Guid.NewGuid(),
        ChannelType = ChannelType.WP,
        Provider = Provider.NetZeroInboxTemplate,
        ProviderName = "WA Template",
        Label = "WA Template",
        LabelSlug = "watemplate",
        IsActive = true,
        ConfigJson = """{"templateToken":"tok"}""",
    };

    private static (ChannelDispatchService Service, FakeDispatchJobRepository Jobs,
        FakeDispatchJobRecipientRepository Recipients, StubTemplateSender Sender) Build(
        WhatsAppTemplateSendResult result)
    {
        var jobs = new FakeDispatchJobRepository();
        var recipients = new FakeDispatchJobRecipientRepository();
        var sender = new StubTemplateSender(result);

        var service = new ChannelDispatchService(
            new FakeMessageRequestRepository(),
            jobs,
            recipients,
            new FakeChannelConfigRepository(TemplateChannel()),
            new PlainTextEncryption(),
            new FakeSenderFactory(new UnreachableSender()),
            sender,
            new NoOpFallbackHandler(),
            NullLogger<ChannelDispatchService>.Instance);

        return (service, jobs, recipients, sender);
    }

    private static ParsedChannelJob TemplateJob() => new()
    {
        ChannelType = ChannelType.WP,
        Message = "Template 'order_confirmation' with 2 variable(s)",
        TemplateName = "order_confirmation",
        TemplateVariables = ["Jane Doe", "ORD-123"],
        ChannelLabelSlug = "watemplate",
    };

    [Fact]
    public async Task A_successful_template_send_writes_a_sent_job_and_recipient_row()
    {
        var (service, jobs, recipients, sender) = Build(new WhatsAppTemplateSendResult
        {
            IsSuccess = true,
            MessageId = "wamid.123",
            RawResponse = """{"success":true}""",
        });

        await service.DispatchAsync(Guid.NewGuid(), "8801700000001", [TemplateJob()]);

        var job = Assert.Single(jobs.Added);
        Assert.Equal(JobStatus.Sent, job.Status);
        Assert.Equal("wamid.123", job.ProviderMessageId);
        Assert.NotNull(job.SentAt);

        var recipient = Assert.Single(recipients.Added);
        Assert.Equal("8801700000001", recipient.Recipient);

        Assert.Equal("order_confirmation", sender.LastTemplateName);
        Assert.Equal(["Jane Doe", "ORD-123"], sender.LastVariables);
        // The vendor wants E.164 for this call, unlike the canonical stored form.
        Assert.Equal("+8801700000001", sender.LastToNumber);
    }

    [Fact]
    public async Task A_failed_template_send_writes_a_failed_job_with_the_providers_reason()
    {
        var (service, jobs, _, _) = Build(new WhatsAppTemplateSendResult
        {
            IsSuccess = false,
            Error = "template not approved",
            RawResponse = """{"success":false}""",
        });

        await service.DispatchAsync(Guid.NewGuid(), "8801700000001", [TemplateJob()]);

        var job = Assert.Single(jobs.Added);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("template not approved", job.FailureReason);
    }

    [Fact]
    public async Task One_job_per_recipient_for_a_multi_number_template_send()
    {
        var (service, jobs, recipients, _) = Build(new WhatsAppTemplateSendResult
        {
            IsSuccess = true, MessageId = "wamid.1", RawResponse = "{}",
        });

        await service.DispatchAsync(
            Guid.NewGuid(), "8801700000001,8801700000002", [TemplateJob()]);

        Assert.Equal(2, jobs.Added.Count);
        Assert.Equal(2, recipients.Added.Count);
    }
}
