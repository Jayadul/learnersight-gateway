using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

/// <summary>
/// Asks each provider what became of the messages it accepted, and writes the answers
/// onto the recipient rows. Runs nightly, and on demand from a channel's card.
/// </summary>
public class StatusSyncService(
    IChannelConfigRepository configRepo,
    IDispatchJobRecipientRepository recipientRepo,
    IEncryptionService enc,
    IEnumerable<IChannelStatusSyncProvider> providers,
    IConfiguration config,
    ILogger<StatusSyncService> logger
) : IStatusSyncService
{
    private readonly Dictionary<Domain.Enums.Provider, IChannelStatusSyncProvider> _providers =
        providers.ToDictionary(p => p.Provider);

    /// <summary>
    /// Rows resolved per channel per run. Chithi and REVE are looked up per row or per
    /// job, so an unbounded window would mean an unbounded number of provider calls.
    /// </summary>
    private int DefaultLimit => config.GetValue("StatusSync:MaxRowsPerRun", 500);

    public async Task<List<StatusSyncResultDto>> GetSyncableChannelsAsync()
    {
        var configs = await configRepo.GetAllAsync();
        return configs
            .Where(c => _providers.ContainsKey(c.Provider))
            .OrderBy(c => c.ChannelType).ThenBy(c => c.Label)
            .Select(c => new StatusSyncResultDto
            {
                ChannelConfigId = c.Id,
                Label = c.Label,
                Success = true
            })
            .ToList();
    }

    public async Task<StatusSyncResultDto> SyncChannelAsync(
        Guid channelConfigId, StatusSyncRequest request)
    {
        var config = await configRepo.GetByIdAsync(channelConfigId)
            ?? throw new NotFoundException($"ChannelConfig {channelConfigId} not found.");

        if (!_providers.ContainsKey(config.Provider))
        {
            throw new InvalidOperationException(
                $"Provider '{config.Provider}' does not offer a delivery status lookup. "
                + "Status sync is available for REVE, Chithi and Net ZerO Voice channels.");
        }

        var (from, to, allWaiting) = ResolveWindow(request);
        var result = await SyncOneAsync(
            config, from, to, allWaiting, request.Limit ?? (allWaiting ? ManualLimit : DefaultLimit));
        await recipientRepo.SaveChangesAsync();
        return result;
    }

    public async Task SyncAllAsync()
    {
        // The nightly run asks about every row still waiting, whenever it was sent: rows leave the
        // waiting set once answered or given up on, so it stays small, and a night the job missed
        // is picked up by the next one.
        var (from, to) = (DateTimeOffset.UnixEpoch, DateTimeOffset.UtcNow.AddMinutes(1));

        var configs = (await configRepo.GetAllAsync())
            .Where(c => c.IsActive && _providers.ContainsKey(c.Provider))
            .ToList();

        logger.LogInformation(
            "[STATUS_SYNC] Starting for {Count} channels, every waiting recipient", configs.Count);

        foreach (var config in configs)
        {
            // One unreachable provider must not stop the rest.
            try
            {
                var result = await SyncOneAsync(config, from, to, allWaiting: true, DefaultLimit);
                logger.LogInformation(
                    "[STATUS_SYNC] {Label}: updated={Updated} unmatched={Unmatched} {Error}",
                    config.Label, result.Updated, result.Unmatched, result.Error ?? "");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[STATUS_SYNC] {Label} failed", config.Label);
            }
        }

        await recipientRepo.SaveChangesAsync();
        logger.LogInformation("[STATUS_SYNC] Complete");
    }

    private async Task<StatusSyncResultDto> SyncOneAsync(
        ChannelConfig config, DateTimeOffset from, DateTimeOffset to, bool allWaiting, int limit)
    {
        var dto = new StatusSyncResultDto
        {
            ChannelConfigId = config.Id,
            Label = config.Label,
            From = from,
            To = to
        };

        // Every waiting row, whenever it was sent (from is the epoch), so nothing awaiting a status
        // is ever outside the query. A custom range (API only) keeps to its own days.
        var rows = await recipientRepo.GetPendingForChannelAsync(config.Id, from, to, limit);
        if (rows.Count == 0)
        {
            dto.Success = true;
            if (allWaiting) { dto.From = DateTimeOffset.UtcNow; dto.To = DateTimeOffset.UtcNow; }
            return dto;
        }
        // There may be more than one run can take; say so instead of implying it is all done.
        dto.MoreWaiting = allWaiting && rows.Count >= limit;

        var cfg = JsonSerializer.Deserialize<Dictionary<string, string>>(
            enc.Decrypt(config.ConfigJson)) ?? [];

        // A provider that reads a report window (Voice) is given the span the waiting rows
        // actually cover, not the whole epoch and not just today.
        var (providerFrom, providerTo) = allWaiting
            ? (rows.Min(r => r.CreatedAt), rows.Max(r => r.CreatedAt))
            : (from, to);
        if (allWaiting) { dto.From = providerFrom; dto.To = providerTo; }
        var before = rows.ToDictionary(r => r.Id, r => r.DeliveryStatus);
        var result = await _providers[config.Provider].SyncAsync(cfg, rows, providerFrom, providerTo);

        dto.Success = result.Success;
        dto.Unmatched = result.Unmatched;
        dto.Error = result.Error;

        // A failed run leaves every row exactly as it was, so nothing is persisted and
        // the next run retries the same window. Only a successful run has changes.
        if (result.Success)
        {
            GiveUpOnStale(rows);
            foreach (var row in rows) recipientRepo.Update(row);

            // Counted from what actually happened to each row, not from how many the provider
            // touched: a row it answered "still in flight" for is not resolved.
            dto.Updated = rows.Count(r => r.DeliveryStatus is DeliveryStatus.Delivered or DeliveryStatus.Failed
                && before[r.Id] != r.DeliveryStatus);
            dto.MarkedUnknown = rows.Count(r => r.DeliveryStatus == DeliveryStatus.Unknown
                && before[r.Id] == DeliveryStatus.Pending);
            dto.StillWaiting = rows.Count(r => r.DeliveryStatus == DeliveryStatus.Pending);
            if (dto.MarkedUnknown > 0)
                logger.LogInformation(
                    "[STATUS_SYNC] {Label}: {Count} rows had no clear answer after {Hours}h, marked Unknown",
                    config.Label, dto.MarkedUnknown, GiveUpAfterHours);
        }

        return dto;
    }

    /// <summary>
    /// A row the provider still has no clear answer for after a full day (a REVE message stuck
    /// at "sent", a call the report never lists) is marked Unknown instead of staying In
    /// Progress indefinitely. Honest rather than guessed: it is not called Delivered.
    /// </summary>
    private int GiveUpOnStale(List<DispatchJobRecipient> rows)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-GiveUpAfterHours);
        var count = 0;
        foreach (var row in rows.Where(r => r.DeliveryStatus == Domain.Enums.DeliveryStatus.Pending))
        {
            var sentAt = row.DispatchJob?.SentAt ?? row.CreatedAt;
            if (sentAt >= cutoff) continue;
            row.DeliveryStatus = Domain.Enums.DeliveryStatus.Unknown;
            row.LastCheckedAt = DateTimeOffset.UtcNow;
            count++;
        }
        return count;
    }

    /// <summary>
    /// Hotfix batch 7: the admin-facing sync is one action with no dates. It asks about every
    /// recipient currently waiting for a status, whenever it was sent. A date-bounded window
    /// never selected older waiting rows, which is what left them In Progress forever.
    /// <list type="bullet">
    /// <item>No dates: every waiting row (the "Sync Now" button and the nightly job).</item>
    /// <item>Dates given (API only, no UI): exactly those Dhaka days, snapped outward to whole days.</item>
    /// </list>
    /// <para>
    /// The bounds are UTC instants (see <see cref="AppTime.StartOfDayUtc"/>): a
    /// display-converted +06:00 offset must never reach dispatch_job_recipients' UTC columns,
    /// which Npgsql refuses to write (the batch 3 crash that stopped every sync run).
    /// </para>
    /// </summary>
    private (DateTimeOffset From, DateTimeOffset To, bool AllWaiting) ResolveWindow(StatusSyncRequest request)
    {
        if (request.From is { } explicitFrom)
        {
            var start = DayBounds(explicitFrom).From;
            var end = DayBounds(request.To ?? DateTimeOffset.UtcNow).To;
            return (start, end < start ? DayBounds(explicitFrom).To : end, false);
        }

        return (DateTimeOffset.UnixEpoch, DateTimeOffset.UtcNow.AddMinutes(1), true);
    }

    /// <summary>The business-zone calendar day containing the instant, as UTC bounds (last millisecond inclusive).</summary>
    private (DateTimeOffset From, DateTimeOffset To) DayBounds(DateTimeOffset instant)
    {
        var start = AppTime.StartOfDayUtc(instant, AppTime.Resolve(config));
        return (start, start.AddDays(1).AddMilliseconds(-1));
    }

    /// <summary>Rows one manual Sync Now takes per channel; if more wait, the result says so.</summary>
    private int ManualLimit => config.GetValue("StatusSync:ManualMaxRows", 2000);
    /// <summary>
    /// How long a message may stay in flight before it is called Status Unknown. A delivery report
    /// normally arrives within minutes, so a day (the old value) left an unanswered message In
    /// Progress long after a sync had asked about it. A late report is still picked up: Unknown rows
    /// from the last 72 hours are asked about again (see the recipient repository).
    /// </summary>
    private int GiveUpAfterHours => config.GetValue("StatusSync:GiveUpAfterHours", 6);
}
