using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;

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
                + "Status sync is available for REVE, Chithi and NetZero Voice channels.");
        }

        var (from, to, carryOver) = ResolveWindow(request);
        var result = await SyncOneAsync(config, from, to, carryOver, request.Limit ?? DefaultLimit);
        await recipientRepo.SaveChangesAsync();
        return result;
    }

    public async Task SyncAllAsync()
    {
        // The nightly run covers the whole previous business day, plus any older row still
        // waiting on an answer — which also backfills a night the job missed.
        var yesterday = DayBounds(DateTimeOffset.UtcNow.AddDays(-1));
        var (from, to) = yesterday;

        var configs = (await configRepo.GetAllAsync())
            .Where(c => c.IsActive && _providers.ContainsKey(c.Provider))
            .ToList();

        logger.LogInformation(
            "[STATUS_SYNC] Starting for {Count} channels, window {From:u}..{To:u} plus carry-over",
            configs.Count, from, to);

        foreach (var config in configs)
        {
            // One unreachable provider must not stop the rest.
            try
            {
                var result = await SyncOneAsync(config, from, to, carryOver: true, DefaultLimit);
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
        ChannelConfig config, DateTimeOffset from, DateTimeOffset to, bool carryOver, int limit)
    {
        var dto = new StatusSyncResultDto
        {
            ChannelConfigId = config.Id,
            Label = config.Label,
            From = from,
            To = to
        };

        // Rows still waiting on an answer from earlier days are asked about again. Without
        // this a message a provider first reports as "sent" (REVE, Chithi) is never looked at
        // once its own day has passed, and stays In Progress forever.
        var queryFrom = carryOver ? from.AddDays(-CarryOverDays) : from;
        var rows = await recipientRepo.GetPendingForChannelAsync(config.Id, queryFrom, to, limit);
        if (rows.Count == 0)
        {
            dto.Success = true;
            return dto;
        }

        var cfg = JsonSerializer.Deserialize<Dictionary<string, string>>(
            enc.Decrypt(config.ConfigJson)) ?? [];

        // A provider that reads a report window (Voice) has to cover the carried-over rows too.
        var providerFrom = rows.Min(r => r.CreatedAt) is var oldest && oldest < from ? oldest : from;
        var result = await _providers[config.Provider].SyncAsync(cfg, rows, providerFrom, to);

        dto.Success = result.Success;
        dto.Updated = result.Updated;
        dto.Unmatched = result.Unmatched;
        dto.Error = result.Error;

        // A failed run leaves every row exactly as it was, so nothing is persisted and
        // the next run retries the same window. Only a successful run has changes.
        if (result.Success)
        {
            var gaveUp = GiveUpOnStale(rows);
            if (gaveUp > 0)
                logger.LogInformation(
                    "[STATUS_SYNC] {Label}: {Count} rows had no clear answer after {Hours}h, marked Unknown",
                    config.Label, gaveUp, GiveUpAfterHours);
            foreach (var row in rows) recipientRepo.Update(row);
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
    /// Every window is a complete business-zone calendar day, 00:00:00 to 23:59:59, so the
    /// nightly job and a manual sync can never disagree about what a day covers.
    /// <list type="bullet">
    /// <item>No dates: today, plus carry-over of older rows still waiting (the "sync today" action).</item>
    /// <item>Dates given: exactly those days, snapped outward to whole days, with no carry-over
    /// (a deliberate historical backfill).</item>
    /// </list>
    /// <para>
    /// The bounds are UTC instants (see <see cref="AppTime.StartOfDayUtc"/>): a
    /// display-converted +06:00 offset must never reach dispatch_job_recipients' UTC columns,
    /// which Npgsql refuses to write (the batch 3 crash that stopped every sync run).
    /// </para>
    /// </summary>
    private (DateTimeOffset From, DateTimeOffset To, bool CarryOver) ResolveWindow(StatusSyncRequest request)
    {
        if (request.From is { } explicitFrom)
        {
            var start = DayBounds(explicitFrom).From;
            var end = DayBounds(request.To ?? DateTimeOffset.UtcNow).To;
            return (start, end < start ? DayBounds(explicitFrom).To : end, false);
        }

        var today = DayBounds(DateTimeOffset.UtcNow);
        return (today.From, today.To, true);
    }

    /// <summary>The business-zone calendar day containing the instant, as UTC bounds (last millisecond inclusive).</summary>
    private (DateTimeOffset From, DateTimeOffset To) DayBounds(DateTimeOffset instant)
    {
        var start = AppTime.StartOfDayUtc(instant, AppTime.Resolve(config));
        return (start, start.AddDays(1).AddMilliseconds(-1));
    }

    private int CarryOverDays => config.GetValue("StatusSync:CarryOverDays", 30);
    private int GiveUpAfterHours => config.GetValue("StatusSync:GiveUpAfterHours", 24);
}
