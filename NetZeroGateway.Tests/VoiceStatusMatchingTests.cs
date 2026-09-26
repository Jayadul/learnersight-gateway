using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using NetZeroGateway.Infrastructure.StatusSync;

namespace NetZeroGateway.Tests;

/// <summary>
/// Real Voice status never resolved (hotfix batch 6). Evidence from the live provider: the
/// report writes <c>StartTime</c> as <c>2026-09-25 08:45:51.629+06</c> (with an offset), and its
/// call times sit 6 to 9 minutes away from our dispatch time. The offset was dropped and the
/// clock re-read in the server's zone, and the 5 minute tolerance was too tight either way.
/// </summary>
public class VoiceStatusMatchingTests
{
    private static readonly DateTimeOffset Dispatched = new(2026, 9, 25, 2, 54, 50, TimeSpan.Zero);

    private sealed class FakeReport(params NetZeroVoiceCallRecord[] records) : INetZeroVoiceReportClient
    {
        public Task<NetZeroVoiceReportResponse> GetCombinedReportAsync(
            string owner, string password, DateTimeOffset from, DateTimeOffset to,
            int offset, int limit, string? baseUrl = null, CancellationToken ct = default)
            => Task.FromResult(new NetZeroVoiceReportResponse
            {
                Status = "success",
                Data = offset == 0 ? [.. records] : []
            });
    }

    private static NetZeroVoiceStatusSyncProvider Provider(params NetZeroVoiceCallRecord[] records)
        => new(new FakeReport(records),
               new ConfigurationBuilder().AddInMemoryCollection(
                   new Dictionary<string, string?> { ["App:TimeZone"] = "Asia/Dhaka" }).Build(),
               NullLogger<NetZeroVoiceStatusSyncProvider>.Instance);

    private static DispatchJobRecipient Row(DateTimeOffset at, string number = "8801748848039") => new()
    {
        Id = Guid.NewGuid(),
        Recipient = number,
        DeliveryStatus = DeliveryStatus.Pending,
        CreatedAt = at,
        DispatchJob = new DispatchJob { SentAt = at, ToNumber = number }
    };

    private static NetZeroVoiceCallRecord Record(string start, string state = "CALL SUCCESS", string number = "8801748848039")
        => new() { Destination = number, StartTime = start, CallState = state };

    private static Dictionary<string, string> Cfg() => new() { ["owner"] = "o", ["password"] = "p" };

    [Fact]
    public async Task The_real_report_shape_matches_and_resolves_the_call()
    {
        // Exactly what the live report returned for the 2026-09-25 test call: 02:45:51Z, nine
        // minutes before our recorded dispatch.
        var rows = new List<DispatchJobRecipient> { Row(Dispatched) };

        var result = await Provider(Record("2026-09-25 08:45:51.629+06"))
            .SyncAsync(Cfg(), rows, Dispatched.AddHours(-1), Dispatched.AddHours(1));

        Assert.Equal(1, result.Updated);
        Assert.Equal(DeliveryStatus.Delivered, rows[0].DeliveryStatus);
    }

    [Fact]
    public async Task An_offset_is_read_as_the_instant_it_states_not_as_server_local_time()
    {
        // 08:55 +06 is 02:55Z, one minute after dispatch. Reading the digits in a UTC server's
        // zone and then calling them Dhaka time would put it six hours away.
        var rows = new List<DispatchJobRecipient> { Row(Dispatched) };

        await Provider(Record("2026-09-25 08:55:00+06"))
            .SyncAsync(Cfg(), rows, Dispatched.AddHours(-1), Dispatched.AddHours(1));

        Assert.Equal(DeliveryStatus.Delivered, rows[0].DeliveryStatus);
    }

    [Fact]
    public async Task A_time_without_an_offset_is_still_read_as_business_zone_wall_clock()
    {
        var rows = new List<DispatchJobRecipient> { Row(Dispatched) };

        await Provider(Record("2026-09-25 08:55:00"))
            .SyncAsync(Cfg(), rows, Dispatched.AddHours(-1), Dispatched.AddHours(1));

        Assert.Equal(DeliveryStatus.Delivered, rows[0].DeliveryStatus);
    }

    [Fact]
    public async Task A_failed_call_state_resolves_to_failed()
    {
        var rows = new List<DispatchJobRecipient> { Row(Dispatched) };

        await Provider(Record("2026-09-25 08:55:00+06", "CALL FAILED"))
            .SyncAsync(Cfg(), rows, Dispatched.AddHours(-1), Dispatched.AddHours(1));

        Assert.Equal(DeliveryStatus.Failed, rows[0].DeliveryStatus);
    }

    [Fact]
    public async Task Two_calls_to_one_number_are_paired_with_the_two_rows_in_time_order()
    {
        var first = Row(Dispatched);
        var second = Row(Dispatched.AddMinutes(12));
        var rows = new List<DispatchJobRecipient> { second, first };

        // Report order is deliberately reversed: pairing is by time, not by list position.
        await Provider(
                Record("2026-09-25 09:03:00+06", "CALL FAILED"),
                Record("2026-09-25 08:52:00+06", "CALL SUCCESS"))
            .SyncAsync(Cfg(), rows, Dispatched.AddHours(-1), Dispatched.AddHours(1));

        Assert.Equal(DeliveryStatus.Delivered, first.DeliveryStatus);
        Assert.Equal(DeliveryStatus.Failed, second.DeliveryStatus);
    }

    [Fact]
    public async Task A_different_number_never_matches()
    {
        var rows = new List<DispatchJobRecipient> { Row(Dispatched) };

        var result = await Provider(Record("2026-09-25 08:55:00+06", number: "8801999999999"))
            .SyncAsync(Cfg(), rows, Dispatched.AddHours(-1), Dispatched.AddHours(1));

        Assert.Equal(0, result.Updated);
        Assert.Equal(DeliveryStatus.Pending, rows[0].DeliveryStatus);
    }

    [Fact]
    public async Task More_records_than_rows_is_left_alone_rather_than_guessed()
    {
        var rows = new List<DispatchJobRecipient> { Row(Dispatched) };

        var result = await Provider(
                Record("2026-09-25 08:52:00+06"),
                Record("2026-09-25 08:58:00+06", "CALL FAILED"))
            .SyncAsync(Cfg(), rows, Dispatched.AddHours(-1), Dispatched.AddHours(1));

        Assert.Equal(0, result.Updated);
        Assert.Equal(DeliveryStatus.Pending, rows[0].DeliveryStatus);
    }
}
