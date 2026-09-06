using System.Globalization;
using System.Text.Json;
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
    /// the same call. Wide enough for provider queueing, narrow enough that two
    /// campaigns to one number in an hour do not collide.
    /// </summary>
    private TimeSpan MatchTolerance => TimeSpan.FromMinutes(
        config.GetValue("Providers:Voice:StatusMatchToleranceMinutes", 5));

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

        // Widen the pull by the tolerance at both ends: a call dispatched just inside
        // the window can be reported just outside it.
        var records = new List<HttpClients.Models.NetZeroVoiceCallRecord>();
        for (var page = 0; page < MaxPages; page++)
        {
            var r = await client.GetCombinedReportAsync(
                owner, password, from - MatchTolerance, to + MatchTolerance,
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
            if (r.TotalRecords is { } total && records.Count >= total) break;
        }

        logger.LogInformation("[VOICE_SYNC] Pulled {Count} call records", records.Count);

        var updated = 0;
        var unmatched = 0;

        foreach (var row in rows)
        {
            var dispatchedAt = row.DispatchJob?.SentAt ?? row.CreatedAt;
            var candidates = records
                .Where(rec => Matches(rec, row, dispatchedAt, tz))
                .ToList();

            if (candidates.Count == 0)
            {
                // Normal for a call still in progress; also what a wrong matching rule
                // looks like, which is why the count is reported.
                unmatched++;
                continue;
            }

            if (candidates.Count > 1)
            {
                logger.LogWarning(
                    "[VOICE_SYNC] {Count} report records match {Recipient} around {At} — "
                    + "left unresolved for manual review rather than guessing.",
                    candidates.Count, row.Recipient, dispatchedAt);
                unmatched++;
                continue;
            }

            var match = candidates[0];
            row.DeliveryStatus = MapState(match.CallState);
            row.RawStatusResponse = JsonSerializer.Serialize(match);
            row.LastCheckedAt = DateTimeOffset.UtcNow;
            updated++;
        }

        return StatusSyncResult.Ok(updated, unmatched);
    }

    /// <summary>
    /// Number plus time window. The report writes destinations in whatever shape the
    /// provider uses, so both sides are normalised before comparing.
    /// </summary>
    private bool Matches(
        HttpClients.Models.NetZeroVoiceCallRecord record,
        DispatchJobRecipient row,
        DateTimeOffset dispatchedAt,
        TimeZoneInfo tz)
    {
        if (string.IsNullOrWhiteSpace(record.Destination)) return false;
        if (RecipientFormat.ToCanonical(record.Destination)
            != RecipientFormat.ToCanonical(row.Recipient)) return false;

        if (ParseReportTime(record.StartTime, tz) is not { } startedAt) return false;

        return (startedAt - dispatchedAt).Duration() <= MatchTolerance;
    }

    /// <summary>
    /// Report times are business-zone wall clock with no offset, so they are read in
    /// that zone and converted to UTC before comparison — parsing them as UTC would
    /// shift every call by six hours and match nothing.
    /// </summary>
    private static DateTimeOffset? ParseReportTime(string? value, TimeZoneInfo tz)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (!DateTime.TryParse(
                value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return null;

        return new DateTimeOffset(
            DateTime.SpecifyKind(local, DateTimeKind.Unspecified),
            tz.GetUtcOffset(local));
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
