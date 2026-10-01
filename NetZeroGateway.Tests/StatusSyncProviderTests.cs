using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using NetZeroGateway.Infrastructure.StatusSync;

namespace NetZeroGateway.Tests;

/// <summary>
/// What each provider writes back onto a recipient row.
/// <para>
/// The distinction these tests exist for: a recipient the provider says failed is a
/// delivery failure and belongs on the row, while a request the provider rejected says
/// nothing about any recipient and must leave every row untouched. Conflating them
/// writes "undelivered" against messages that actually arrived.
/// </para>
/// </summary>
public class StatusSyncProviderTests
{
    private static readonly DateTimeOffset DispatchedAt =
        new(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);

    private static DispatchJobRecipient Row(
        string recipient = "8801748848039", string? reference = "REF1") => new()
    {
        Id = Guid.NewGuid(),
        Recipient = recipient,
        ProviderReferenceId = reference,
        DeliveryStatus = DeliveryStatus.Pending,
        CreatedAt = DispatchedAt,
        DispatchJob = new DispatchJob { SentAt = DispatchedAt, ToNumber = recipient }
    };

    private static IConfiguration Config(params (string Key, string Value)[] extra)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(
                extra.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))
            {
                ["App:TimeZone"] = "Asia/Dhaka"
            })
            .Build();

    // ── REVE ─────────────────────────────────────────────────────────────────

    private sealed class FakeReveStatusClient(ReveStatusResponse response) : IReveStatusClient
    {
        public int Calls { get; private set; }
        public Task<ReveStatusResponse> GetStatusAsync(
            string apiKey, string secretKey, string messageId,
            string? baseUrl = null, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(response);
        }
    }

    private static Dictionary<string, string> ReveCfg() =>
        new() { ["apikey"] = "k", ["secretkey"] = "s" };

    [Fact]
    public async Task REVE_maps_DELIVRD_to_delivered()
    {
        var provider = new ReveStatusSyncProvider(
            new FakeReveStatusClient(new ReveStatusResponse { Status = "0", Text = "DELIVRD" }),
            NullLogger<ReveStatusSyncProvider>.Instance);
        var rows = new List<DispatchJobRecipient> { Row() };

        var result = await provider.SyncAsync(ReveCfg(), rows, DispatchedAt, DispatchedAt);

        Assert.True(result.Success);
        Assert.Equal(DeliveryStatus.Delivered, rows[0].DeliveryStatus);
        Assert.NotNull(rows[0].LastCheckedAt);
    }

    [Theory]
    [InlineData("114")] // message id missing, or already queried once before
    [InlineData("109")] // no apikey
    [InlineData("108")] // wrong password
    [InlineData("101")] // internal server error
    [InlineData("-42")] // auth failed
    [InlineData("1")]   // generic "request failed"
    public async Task REVE_treats_a_rejected_lookup_as_unknown_not_a_delivery_failure(string statusCode)
    {
        // Hotfix batch 9, Task 4: every one of REVE's own documented REJECTD codes is a
        // rejection of the STATUS QUERY, never a statement that the SMS failed —
        // confirmed live against a resend REVE had actually accepted (ACCEPTD) that
        // later read back Status 114/REJECTD and was being recorded Failed on that
        // alone. Blanket-mapping REJECTD to Failed was the bug; every one of these codes
        // must land Unknown instead, exactly like Chithi's aged-out reference ids.
        var provider = new ReveStatusSyncProvider(
            new FakeReveStatusClient(new ReveStatusResponse { Status = statusCode, Text = "REJECTD" }),
            NullLogger<ReveStatusSyncProvider>.Instance);
        var rows = new List<DispatchJobRecipient> { Row() };

        await provider.SyncAsync(ReveCfg(), rows, DispatchedAt, DispatchedAt);

        Assert.Equal(DeliveryStatus.Unknown, rows[0].DeliveryStatus);
        // The raw payload is kept so the rejected code stays visible for review.
        Assert.Contains(statusCode, rows[0].RawStatusResponse);
        Assert.NotNull(rows[0].LastCheckedAt);
    }

    [Theory]
    [InlineData("EXPIRED")]
    [InlineData("UNDELIV")]
    [InlineData("DELETED")]
    public async Task REVE_still_maps_a_genuine_terminal_failure_word_to_failed(string text)
    {
        // Unlike REJECTD, these words describe what happened to the SMS itself, not the
        // status query — they must keep failing the row.
        var provider = new ReveStatusSyncProvider(
            new FakeReveStatusClient(new ReveStatusResponse { Status = "0", Text = text }),
            NullLogger<ReveStatusSyncProvider>.Instance);
        var rows = new List<DispatchJobRecipient> { Row() };

        await provider.SyncAsync(ReveCfg(), rows, DispatchedAt, DispatchedAt);

        Assert.Equal(DeliveryStatus.Failed, rows[0].DeliveryStatus);
    }

    [Fact]
    public async Task REVE_leaves_an_unrecognised_state_unknown_rather_than_guessing_failed()
    {
        // A state genuinely not in the mapping. ACCEPTD used to stand in for this and no
        // longer can — it is a real in-flight state, covered by the theory below.
        var provider = new ReveStatusSyncProvider(
            new FakeReveStatusClient(new ReveStatusResponse { Status = "9", Text = "UNKNOWNSTATE" }),
            NullLogger<ReveStatusSyncProvider>.Instance);
        var rows = new List<DispatchJobRecipient> { Row() };

        await provider.SyncAsync(ReveCfg(), rows, DispatchedAt, DispatchedAt);

        Assert.Equal(DeliveryStatus.Unknown, rows[0].DeliveryStatus);
    }

    [Theory]
    [InlineData("SENT")]
    [InlineData("ACCEPTD")]
    [InlineData("ENROUTE")]
    public async Task An_in_flight_REVE_state_stays_pending_so_the_next_run_asks_again(
        string state)
    {
        // The sync only revisits Pending rows. Recording an in-flight state as Unknown
        // strands the message: it would have resolved to DELIVRD on a later run, but
        // nothing ever asks about it again.
        var provider = new ReveStatusSyncProvider(
            new FakeReveStatusClient(new ReveStatusResponse { Status = "4", Text = state }),
            NullLogger<ReveStatusSyncProvider>.Instance);
        var rows = new List<DispatchJobRecipient> { Row() };

        await provider.SyncAsync(ReveCfg(), rows, DispatchedAt, DispatchedAt);

        Assert.Equal(DeliveryStatus.Pending, rows[0].DeliveryStatus);
    }

    [Fact]
    public async Task REVE_looks_up_a_batch_id_once_not_once_per_recipient()
    {
        // REVE returns one id for a whole job. A 500-recipient batch must not become
        // 500 identical lookups.
        var client = new FakeReveStatusClient(
            new ReveStatusResponse { Status = "0", Text = "DELIVRD" });
        var provider = new ReveStatusSyncProvider(
            client, NullLogger<ReveStatusSyncProvider>.Instance);

        var rows = new List<DispatchJobRecipient>
        {
            Row("8801748848039", "SHARED"),
            Row("8801969155575", "SHARED"),
            Row("8801700000003", "SHARED"),
        };

        var result = await provider.SyncAsync(ReveCfg(), rows, DispatchedAt, DispatchedAt);

        Assert.Equal(1, client.Calls);
        Assert.Equal(3, result.Updated);
        Assert.All(rows, r => Assert.Equal(DeliveryStatus.Delivered, r.DeliveryStatus));
    }

    // ── Chithi ───────────────────────────────────────────────────────────────

    private sealed class FakeChithiStatusClient(ChithiStatusResponse response) : IChithiStatusClient
    {
        public Task<ChithiStatusResponse> GetStatusAsync(
            string apiToken, string referenceId,
            string? baseUrl = null, CancellationToken ct = default)
            => Task.FromResult(response);
    }

    private static Dictionary<string, string> ChithiCfg() => new() { ["api_token"] = "t" };

    [Fact]
    public async Task Chithi_maps_Success_to_delivered()
    {
        var provider = new ChithiStatusSyncProvider(
            new FakeChithiStatusClient(new ChithiStatusResponse
            {
                Status = "SUCCESS",
                SmsInfo = [new ChithiStatusInfo { SmsStatus = "Success" }]
            }),
            NullLogger<ChithiStatusSyncProvider>.Instance);
        var rows = new List<DispatchJobRecipient> { Row() };

        await provider.SyncAsync(ChithiCfg(), rows, DispatchedAt, DispatchedAt);

        Assert.Equal(DeliveryStatus.Delivered, rows[0].DeliveryStatus);
    }

    [Fact]
    public async Task Chithi_treats_a_rejected_lookup_as_unknown_not_a_delivery_failure()
    {
        // FAILED with an empty smsinfo means the reference id was rejected — Chithi
        // ages them out. That is not the message failing to arrive.
        var provider = new ChithiStatusSyncProvider(
            new FakeChithiStatusClient(new ChithiStatusResponse
            {
                Status = "FAILED",
                StatusCode = 4001,
                ErrorMessage = "Validation Error",
                SmsInfo = []
            }),
            NullLogger<ChithiStatusSyncProvider>.Instance);
        var rows = new List<DispatchJobRecipient> { Row() };

        await provider.SyncAsync(ChithiCfg(), rows, DispatchedAt, DispatchedAt);

        Assert.Equal(DeliveryStatus.Unknown, rows[0].DeliveryStatus);
        Assert.Contains("Validation Error", rows[0].RawStatusResponse);
    }

    // ── Voice ────────────────────────────────────────────────────────────────

    private sealed class FakeVoiceReportClient(NetZeroVoiceReportResponse response)
        : INetZeroVoiceReportClient
    {
        public Task<NetZeroVoiceReportResponse> GetCombinedReportAsync(
            string owner, string password, DateTimeOffset from, DateTimeOffset to,
            int offset, int limit, string? baseUrl = null, CancellationToken ct = default)
            // Only the first page carries rows; the provider signals the end by
            // returning fewer than a full page.
            => Task.FromResult(offset == 0 ? response : new NetZeroVoiceReportResponse { Data = [] });
    }

    private static Dictionary<string, string> VoiceCfg() =>
        new() { ["owner"] = "o", ["password"] = "p" };

    private static NetZeroVoiceStatusSyncProvider VoiceProvider(
        NetZeroVoiceReportResponse response)
        => new(new FakeVoiceReportClient(response), Config(),
               NullLogger<NetZeroVoiceStatusSyncProvider>.Instance);

    /// <summary>Report times are business-zone wall clock with no offset.</summary>
    private static string ReportTime(DateTimeOffset utc)
        => TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka"))
            .ToString("yyyy-MM-dd HH:mm:ss");

    [Fact]
    public async Task Voice_matches_a_record_by_number_and_time_and_maps_CALL_SUCCESS()
    {
        var provider = VoiceProvider(new NetZeroVoiceReportResponse
        {
            Status = "success",
            Data =
            [
                new NetZeroVoiceCallRecord
                {
                    Destination = "8801748848039",
                    StartTime = ReportTime(DispatchedAt.AddMinutes(1)),
                    CallState = "CALL SUCCESS"
                }
            ]
        });
        var rows = new List<DispatchJobRecipient> { Row() };

        var result = await provider.SyncAsync(
            VoiceCfg(), rows, DispatchedAt.AddHours(-1), DispatchedAt.AddHours(1));

        Assert.True(result.Success);
        Assert.Equal(DeliveryStatus.Delivered, rows[0].DeliveryStatus);
    }

    [Fact]
    public async Task Voice_captures_start_connect_and_end_time_for_a_connected_call()
    {
        // Hotfix batch 9, Task 8: the report already carries these; they were parsed and
        // then discarded. A call that never connected would report no ConnectTime at all
        // (covered separately below), but this one did.
        var connectTime = ReportTime(DispatchedAt.AddMinutes(1).AddSeconds(5));
        var endTime = ReportTime(DispatchedAt.AddMinutes(2));
        var provider = VoiceProvider(new NetZeroVoiceReportResponse
        {
            Status = "success",
            Data =
            [
                new NetZeroVoiceCallRecord
                {
                    Destination = "8801748848039",
                    StartTime = ReportTime(DispatchedAt.AddMinutes(1)),
                    ConnectTime = connectTime,
                    EndTime = endTime,
                    CallState = "CALL SUCCESS"
                }
            ]
        });
        var rows = new List<DispatchJobRecipient> { Row() };

        await provider.SyncAsync(VoiceCfg(), rows, DispatchedAt.AddHours(-1), DispatchedAt.AddHours(1));

        Assert.NotNull(rows[0].CallStartTime);
        Assert.NotNull(rows[0].CallConnectTime);
        Assert.NotNull(rows[0].CallEndTime);
        Assert.True(rows[0].CallEndTime > rows[0].CallConnectTime);
    }

    [Fact]
    public async Task Voice_leaves_ConnectTime_null_for_a_call_that_never_connected()
    {
        var provider = VoiceProvider(new NetZeroVoiceReportResponse
        {
            Status = "success",
            Data =
            [
                new NetZeroVoiceCallRecord
                {
                    Destination = "8801748848039",
                    StartTime = ReportTime(DispatchedAt.AddMinutes(1)),
                    ConnectTime = null,
                    CallState = "CALL FAILED"
                }
            ]
        });
        var rows = new List<DispatchJobRecipient> { Row() };

        await provider.SyncAsync(VoiceCfg(), rows, DispatchedAt.AddHours(-1), DispatchedAt.AddHours(1));

        Assert.NotNull(rows[0].CallStartTime);
        Assert.Null(rows[0].CallConnectTime);
    }

    [Fact]
    public async Task Voice_leaves_a_record_outside_the_tolerance_unmatched()
    {
        // Same number, but hours from the dispatch — a different call entirely.
        var provider = VoiceProvider(new NetZeroVoiceReportResponse
        {
            Status = "success",
            Data =
            [
                new NetZeroVoiceCallRecord
                {
                    Destination = "8801748848039",
                    StartTime = ReportTime(DispatchedAt.AddHours(3)),
                    CallState = "CALL SUCCESS"
                }
            ]
        });
        var rows = new List<DispatchJobRecipient> { Row() };

        var result = await provider.SyncAsync(
            VoiceCfg(), rows, DispatchedAt.AddHours(-1), DispatchedAt.AddHours(6));

        Assert.Equal(0, result.Updated);
        Assert.Equal(1, result.Unmatched);
        Assert.Equal(DeliveryStatus.Pending, rows[0].DeliveryStatus);
    }

    [Fact]
    public async Task Voice_refuses_to_guess_when_two_records_match_the_same_row()
    {
        // Two calls to one number inside the tolerance. Picking either could write the
        // wrong outcome against a real message, so neither is used.
        var provider = VoiceProvider(new NetZeroVoiceReportResponse
        {
            Status = "success",
            Data =
            [
                new NetZeroVoiceCallRecord
                {
                    Destination = "8801748848039",
                    StartTime = ReportTime(DispatchedAt.AddMinutes(1)),
                    CallState = "CALL SUCCESS"
                },
                new NetZeroVoiceCallRecord
                {
                    Destination = "8801748848039",
                    StartTime = ReportTime(DispatchedAt.AddMinutes(2)),
                    CallState = "CALL FAILED"
                }
            ]
        });
        var rows = new List<DispatchJobRecipient> { Row() };

        var result = await provider.SyncAsync(
            VoiceCfg(), rows, DispatchedAt.AddHours(-1), DispatchedAt.AddHours(1));

        Assert.Equal(0, result.Updated);
        Assert.Equal(1, result.Unmatched);
        Assert.Equal(DeliveryStatus.Pending, rows[0].DeliveryStatus);
    }

    [Fact]
    public async Task A_rejected_report_request_touches_no_row_at_all()
    {
        // "session Expired" is the whole request failing, not any call. Every row must
        // keep its status AND its LastCheckedAt so the next run retries this window.
        var provider = VoiceProvider(new NetZeroVoiceReportResponse
        {
            Status = "failure",
            StatusCode = -999999,
            Message = "session Expired"
        });
        var rows = new List<DispatchJobRecipient> { Row() };

        var result = await provider.SyncAsync(
            VoiceCfg(), rows, DispatchedAt.AddHours(-1), DispatchedAt.AddHours(1));

        Assert.False(result.Success);
        Assert.Contains("session Expired", result.Error);
        Assert.Equal(DeliveryStatus.Pending, rows[0].DeliveryStatus);
        Assert.Null(rows[0].LastCheckedAt);
    }

    [Fact]
    public async Task Voice_records_an_unrecognised_call_state_as_unknown()
    {
        // Busy and no-answer states exist and are not catalogued. Assuming Failed would
        // report calls as broken on a guess.
        var provider = VoiceProvider(new NetZeroVoiceReportResponse
        {
            Status = "success",
            Data =
            [
                new NetZeroVoiceCallRecord
                {
                    Destination = "8801748848039",
                    StartTime = ReportTime(DispatchedAt),
                    CallState = "USER BUSY"
                }
            ]
        });
        var rows = new List<DispatchJobRecipient> { Row() };

        await provider.SyncAsync(
            VoiceCfg(), rows, DispatchedAt.AddHours(-1), DispatchedAt.AddHours(1));

        Assert.Equal(DeliveryStatus.Unknown, rows[0].DeliveryStatus);
    }
}
