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

    /// <summary>
    /// Hotfix batch 9, Task 17: what was actually reported as "fallback SMS raw response and
    /// messageId don't match" turned out to be a pre-R3 backfilled row, not a live fallback send
    /// (confirmed against the live DB — every real dispatch_job_recipients row from a fallback
    /// job that exists today is one of these two). The 2026-09-20 backfill migration deliberately
    /// set provider_reference_id = "backfilled" and left raw_status_response NULL so nobody
    /// mistakes a guess for confirmed delivery — but ResolveProviderReply's later job-level
    /// fallback (added for the "wrong provider reply" fix) then filled the blank with the job's
    /// real raw response, which carries the job's real Message_ID right next to a
    /// ProviderReferenceId field still showing "backfilled". The two fields visibly contradicted
    /// each other, which is exactly the "don't match" symptom.
    /// </summary>
    [Fact]
    public void A_backfilled_row_does_not_leak_the_jobs_real_message_id_as_its_raw_response()
    {
        var row = new DispatchJobRecipient
        {
            Recipient = "8801700000001",
            ProviderReferenceId = "backfilled",
            RawStatusResponse = null,
            DeliveryStatus = DeliveryStatus.Delivered,
            DispatchJob = new DispatchJob
            {
                Status = JobStatus.Sent,
                ProviderMessageId = "1217197838",
                ProviderRawResponse = """{"Status":"0","Text":"ACCEPTD","Message_ID":"1217197838"}""",
            }
        };

        var shown = DispatchJobRecipientService.ResolveRawResponse(row);

        Assert.NotNull(shown);
        Assert.DoesNotContain("1217197838", shown);
        using var doc = JsonDocument.Parse(shown!);
        Assert.True(doc.RootElement.GetProperty("backfilled").GetBoolean());
    }

    [Fact]
    public void A_genuine_fallback_rows_own_reference_id_still_matches_its_own_raw_response()
    {
        var row = new DispatchJobRecipient
        {
            Recipient = "8801700000001",
            ProviderReferenceId = "9988776655",
            RawStatusResponse = null,
            DeliveryStatus = DeliveryStatus.Delivered,
            DispatchJob = new DispatchJob
            {
                Status = JobStatus.Sent,
                IsFallback = true,
                ProviderMessageId = "9988776655",
                ProviderRawResponse = """{"Status":"0","Text":"ACCEPTD","Message_ID":"9988776655"}""",
            }
        };

        var shown = DispatchJobRecipientService.ResolveRawResponse(row);

        Assert.Contains("9988776655", shown);
    }
}
