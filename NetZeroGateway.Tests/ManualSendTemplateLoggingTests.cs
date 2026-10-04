using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>A mutable stand-in — SendTemplateAsync needs AddAsync to actually work, unlike the shared fake's default no-op.</summary>
internal sealed class RecordingMessageRequestRepository : IMessageRequestRepository
{
    public List<MessageRequest> Added { get; } = [];

    public Task AddAsync(MessageRequest entity)
    {
        Added.Add(entity);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync() => Task.FromResult(0);
    public Task<MessageRequest?> GetByIdAsync(Guid id) => Task.FromResult(Added.FirstOrDefault(m => m.Id == id));
    public Task<MessageRequest?> GetWithJobsAsync(Guid id) => Task.FromResult<MessageRequest?>(null);
    public void Update(MessageRequest entity) { }
    public void Delete(MessageRequest entity) => Unused.Fail<bool>();
    public Task<List<MessageRequest>> GetAllAsync() => Unused.Fail<Task<List<MessageRequest>>>();
    public Task<List<MessageRequest>> FindAsync(System.Linq.Expressions.Expression<Func<MessageRequest, bool>> p) => Unused.Fail<Task<List<MessageRequest>>>();
    public Task<(List<MessageRequest> Items, int Total)> GetPagedAsync(
        int page, int size, DateTimeOffset? from, DateTimeOffset? to,
        string? toUser, MessageStatus? status, MessageSource? source, Guid? channelConfigId)
        => Unused.Fail<Task<(List<MessageRequest>, int)>>();
}

/// <summary>
/// Hotfix batch 9, Task 15: "template sends aren't appearing in logs". Investigated and
/// found already working for Manual Send — ManualSendService.SendTemplateAsync has always
/// hand-rolled its own DispatchJob/DispatchJobRecipient writes (confirmed live against
/// real historical sends from hotfix batch 3/4). The real gap was /sendtext, which could
/// not send a template at all before Task 10; now that it can, it uses the same shared
/// ChannelDispatchService helpers every other channel type does. This pins Manual Send's
/// side as a permanent regression guard, since no test previously covered it.
/// </summary>
public class ManualSendTemplateLoggingTests
{
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

    private static ManualSendService Build(
        ChannelConfig channel, FakeDispatchJobRepository jobs,
        FakeDispatchJobRecipientRepository recipients, WhatsAppTemplateSendResult result)
        => new(
            new RecordingMessageRequestRepository(),
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new FakeChannelConfigRepository(channel),
            jobs,
            recipients,
            new PlainTextEncryption(),
            new StubTemplateSender(result),
            NullLogger<ManualSendService>.Instance);

    [Fact]
    public async Task A_successful_manual_template_send_writes_a_job_and_a_recipient_row()
    {
        var jobs = new FakeDispatchJobRepository();
        var recipients = new FakeDispatchJobRecipientRepository();
        var service = Build(TemplateChannel(), jobs, recipients, new WhatsAppTemplateSendResult
        {
            IsSuccess = true, MessageId = "wamid.1", RawResponse = """{"success":true}""",
        });

        await service.SendTemplateAsync(new SendTemplateRequest
        {
            ChannelLabelSlug = "watemplate",
            ToNumber = "8801700000001",
            TemplateName = "order_confirmation",
            ExampleArr = ["Jane Doe", "ORD-123"],
        });

        var job = Assert.Single(jobs.Added);
        Assert.Equal(JobStatus.Sent, job.Status);

        var recipient = Assert.Single(recipients.Added);
        Assert.Equal("8801700000001", recipient.Recipient);
        Assert.Equal(job.Id, recipient.DispatchJobId);
    }

    [Fact]
    public async Task A_failed_manual_template_send_still_writes_a_job_and_a_recipient_row()
    {
        // The exact failure mode Task 15 cares about: a template result that didn't
        // succeed must still be logged, not silently dropped.
        var jobs = new FakeDispatchJobRepository();
        var recipients = new FakeDispatchJobRecipientRepository();
        var service = Build(TemplateChannel(), jobs, recipients, new WhatsAppTemplateSendResult
        {
            IsSuccess = false, Error = "template not approved", RawResponse = """{"success":false}""",
        });

        await service.SendTemplateAsync(new SendTemplateRequest
        {
            ChannelLabelSlug = "watemplate",
            ToNumber = "8801700000001",
            TemplateName = "order_confirmation",
            ExampleArr = ["Jane Doe", "ORD-123"],
        });

        var job = Assert.Single(jobs.Added);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("template not approved", job.FailureReason);
        Assert.Single(recipients.Added);
    }
}
