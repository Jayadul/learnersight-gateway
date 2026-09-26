using System.Text.Json;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// Message Logs for FAILED rows (hotfix batch 8): Sent At was blank because a job's SentAt is only set
/// when the provider accepted it, and the raw response was blank whenever the provider never replied.
/// </summary>
public class RecipientDisplayTests
{
    private static DispatchJobRecipient Failed(DispatchJob job) => new()
    {
        Recipient = "8801748848039", DeliveryStatus = DeliveryStatus.Failed, DispatchJob = job
    };

    [Fact]
    public void A_failed_sends_time_is_the_time_it_was_attempted()
    {
        var attempted = new DateTimeOffset(2026, 9, 26, 4, 0, 0, TimeSpan.Zero);
        var job = new DispatchJob { Status = JobStatus.Failed, SentAt = null, CreatedAt = attempted };

        Assert.Equal(attempted, DispatchJobRecipientService.AttemptTime(job));
    }

    [Fact]
    public void An_accepted_sends_time_is_still_its_sent_time()
    {
        var sent = new DateTimeOffset(2026, 9, 26, 4, 0, 5, TimeSpan.Zero);
        var job = new DispatchJob { Status = JobStatus.Sent, SentAt = sent, CreatedAt = sent.AddSeconds(-5) };

        Assert.Equal(sent, DispatchJobRecipientService.AttemptTime(job));
    }

    [Fact]
    public void A_failure_the_provider_replied_to_shows_the_reply_verbatim()
    {
        var raw = """{"status":"error","message":"Instance ID Invalidated"}""";
        var row = Failed(new DispatchJob { Status = JobStatus.Failed, ProviderRawResponse = raw, FailureReason = "x" });

        Assert.Equal(raw, DispatchJobRecipientService.ResolveRawResponse(row));
    }

    [Fact]
    public void A_failure_with_no_provider_reply_says_so_and_gives_the_reason()
    {
        var row = Failed(new DispatchJob { Status = JobStatus.Failed, FailureReason = "This channel does not support media." });

        var shown = DispatchJobRecipientService.ResolveRawResponse(row);

        Assert.NotNull(shown);
        using var doc = JsonDocument.Parse(shown!);
        Assert.False(doc.RootElement.GetProperty("providerReplied").GetBoolean());
        Assert.Equal("This channel does not support media.", doc.RootElement.GetProperty("failureReason").GetString());
    }

    [Fact]
    public void A_row_still_waiting_with_no_reply_is_left_empty_not_invented()
    {
        var row = new DispatchJobRecipient
        {
            Recipient = "8801748848039", DeliveryStatus = DeliveryStatus.Pending,
            DispatchJob = new DispatchJob { Status = JobStatus.Sent }
        };

        Assert.Null(DispatchJobRecipientService.ResolveRawResponse(row));
    }
}
