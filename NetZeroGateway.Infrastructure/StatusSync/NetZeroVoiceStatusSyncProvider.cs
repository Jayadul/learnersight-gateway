using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;

namespace NetZeroGateway.Infrastructure.StatusSync;

/// <summary>
/// Voice status, matched without a correlation id.
/// <para>
/// The combined report carries no reference to anything we sent — only a destination
/// number, a start time and a call state. So a record is matched to a recipient row by
/// number plus a tolerance window around when we dispatched it. That is best-effort by
/// construction: two calls to one number close together can match ambiguously, and an
/// ambiguous match is left alone and logged rather than guessed, because writing the
/// wrong outcome against a real message is worse than leaving it Pending.
/// </para>
/// </summary>
public class NetZeroVoiceStatusSyncProvider(
    INetZeroVoiceReportClient client,
    IConfiguration config,
    ILogger<NetZeroVoiceStatusSyncProvider> logger
) : IChannelStatusSyncProvider
{
    public Provider Provider => Provider.NetZeroVoice;

    /// <summary>
    /// How far a report record's start time may sit from our dispatch time and still be
    /// the same call. The provider's call times were measured 6 to 9 minutes away from our
    /// dispatch times (real calls, 2026-07-04 and 2026-09-25), so the old 5 minutes never
    /// matched a real call. Two calls to one number closer than this are paired in order.
    /// </summary>
    private TimeSpan MatchTolerance => TimeSpan.FromMinutes(
        config.GetValue("Providers:Voice:StatusMatchToleranceMinutes", 30));

    /// <summary>Report pages; the vendor's own example uses 100.</summary>
    private int PageSize => config.GetValue("Providers:Voice:ReportPageSize", 100);

    /// <summary>Stops a runaway window from paging forever against a large account.</summary>
    private const int MaxPages = 50;

    public async Task<StatusSyncResult> SyncAsync(
        Dictionary<string, string> cfg,
        List<DispatchJobRecipient> rows,
        DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default)
    {
        if (!cfg.TryGetValue("owner", out var owner) || !cfg.TryGetValue("password", out var password))
            return StatusSyncResult.Failed("This channel has no voice credentials configured.");

        var baseUrl = ProviderEndpoint.BaseUrlOf(cfg);
        var tz = AppTime.Resolve(config);

        // The report is pulled in small windows around the waiting rows themselves, widened by
        // the tolerance at both ends (a call dispatched just inside can be reported just outside).
        // One span from the oldest row to the newest would be months wide once Sync Now covers
        // every waiting row, hit the page cap, and leave the oldest calls unmatched.
        var records = new List<HttpClients.Models.NetZeroVoiceCallRecord>();
        foreach (var (windowFrom, windowTo) in WindowsAround(rows, from, to))
        {
            for (var page = 0; page < MaxPages; page++)
            {
                var r = await client.GetCombinedReportAsync(
                    owner, password, windowFrom - MatchTolerance, windowTo + MatchTolerance,
                    page * PageSize, PageSize, baseUrl, ct);

                // A rejected request says nothing about any call. Return without touching a
                // single row — including LastCheckedAt — so the next run retries the same
                // window once the credentials are fixed.
                if (r.IsRequestFailure)
                {
                    var reason = r.Error ?? r.Message ?? "The voice provider rejected the report request.";
                    logger.LogError("[VOICE_SYNC] Report request failed: {Reason}", reason);
                    return StatusSyncResult.Failed(reason);
                }

                records.AddRange(r.Rows);

                if (r.Rows.Count < PageSize) break;
                if (r.TotalRecords is { } total && r.Rows.Count + page * PageSize >= total) break;
            }
        }

        logger.LogInformation("[VOICE_SYNC] Pulled {Count} call records", records.Count);

        var updated = 0;
        var unmatched = 0;

        // Records and rows are paired per number. The report has no reference to anything we
        // sent, so a number's records are matched to that number's rows by time: one to one
        // when the counts agree (in chronological order), otherwise left alone.
        var byNumber = records
            .Where(rec => !string.IsNullOrWhiteSpace(rec.Destination))
            .Select(rec => (Rec: rec, Start: ParseReportTime(rec.StartTime, tz)))
            .Where(x => x.Start is not null)
            .GroupBy(x => RecipientFormat.ToCanonical(x.Rec.Destination!))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var group in rows.GroupBy(r => RecipientFormat.ToCanonical(r.Recipient)))
        {
            var groupRows = group
                .OrderBy(r => r.DispatchJob?.SentAt ?? r.CreatedAt)
                .ToList();

            var candidates = byNumber.TryGetValue(group.Key, out var recs)
                ? recs.Where(x => groupRows.Any(r =>
                        (x.Start!.Value - (r.DispatchJob?.SentAt ?? r.CreatedAt)).Duration() <= MatchTolerance))
                      .OrderBy(x => x.Start)
                      .ToList()
                : [];

            if (candidates.Count == 0)
            {
                // Normal for a call still in progress; also what a wrong matching rule
                // looks like, which is why the count is reported.
                unmatched += groupRows.Count;
                continue;
            }

            if (candidates.Count != groupRows.Count)
            {
                logger.LogWarning(
                    "[VOICE_SYNC] {Records} report records for {Recipient} but {Rows} rows to place — "
                    + "left unresolved for manual review rather than guessing.",
                    candidates.Count, group.Key, groupRows.Count);
                unmatched += groupRows.Count;
                continue;
            }

            for (var i = 0; i < groupRows.Count; i++)
            {
                var match = candidates[i].Rec;
                groupRows[i].DeliveryStatus = MapState(match.CallState);
                groupRows[i].RawStatusResponse = JsonSerializer.Serialize(match);
                groupRows[i].LastCheckedAt = DateTimeOffset.UtcNow;
                updated++;
            }
        }

        return StatusSyncResult.Ok(updated, unmatched);
    }

    /// <summary>
    /// Time spans that cover the rows' dispatch times, split wherever two rows are more than a day
    /// apart, so each report request stays small. No rows (nothing to ask about) falls back to the
    /// window the caller gave.
    /// </summary>
    private static List<(DateTimeOffset From, DateTimeOffset To)> WindowsAround(
        List<DispatchJobRecipient> rows, DateTimeOffset from, DateTimeOffset to)
    {
        var windows = new List<(DateTimeOffset From, DateTimeOffset To)>();
        foreach (var t in rows.Select(r => r.DispatchJob?.SentAt ?? r.CreatedAt).OrderBy(t => t))
        {
            if (windows.Count > 0 && t - windows[^1].To <= TimeSpan.FromDays(1))
                windows[^1] = (windows[^1].From, t);
            else
                windows.Add((t, t));
        }
        if (windows.Count == 0) windows.Add((from, to));
        return windows;
    }

    private static readonly Regex HasOffset = new(
        @"\d{2}:\d{2}(:\d{2}(\.\d+)?)?\s*[+-]\d{2}(:?\d{2})?$", RegexOptions.Compiled);

    /// <summary>
    /// The real report writes times as <c>2026-09-25 08:45:51.629+06</c> — with an offset. Those are
    /// read as the instant they state; the earlier code dropped the offset and re-read the
    /// clock in the server's zone, which put every call six hours out and matched nothing.
    /// A value with no offset is business-zone wall clock and is read in that zone.
    /// </summary>
    private static DateTimeOffset? ParseReportTime(string? value, TimeZoneInfo tz)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (HasOffset.IsMatch(value.Trim()))
        {
            return DateTimeOffset.TryParse(
                value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var stated)
                ? stated.ToUniversalTime()
                : null;
        }

        if (!DateTime.TryParse(
                value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return null;

        return new DateTimeOffset(
            DateTime.SpecifyKind(local, DateTimeKind.Unspecified),
            tz.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>
    /// "CALL SUCCESS" is the only state confirmed by the team lead's samples. Anything
    /// else is logged and left Unknown rather than assumed to be a failure — busy and
    /// no-answer states exist and have not been catalogued, and marking an unrecognised
    /// state Failed would report working calls as broken.
    /// </summary>
    private DeliveryStatus MapState(string? state)
    {
        var normalised = state?.Trim().ToUpperInvariant();

        switch (normalised)
        {
            case "CALL SUCCESS":
            case "SUCCESS":
            case "ANSWERED":
                return DeliveryStatus.Delivered;

            case "CALL FAILED":
            case "FAILED":
                return DeliveryStatus.Failed;

            case null or "":
                return DeliveryStatus.Unknown;

            default:
                logger.LogInformation(
                    "[VOICE_SYNC] Unrecognised CallState '{State}' — recorded as Unknown. "
                    + "Add it to the mapping once its meaning is confirmed.", state);
                return DeliveryStatus.Unknown;
        }
    }
}
