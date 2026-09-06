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

        var (from, to) = ResolveWindow(request);
        var result = await SyncOneAsync(config, from, to, request.Limit ?? DefaultLimit);
        await recipientRepo.SaveChangesAsync();
        return result;
    }

    public async Task SyncAllAsync()
    {
        var (from, to) = ResolveWindow(new StatusSyncRequest());

        var configs = (await configRepo.GetAllAsync())
            .Where(c => c.IsActive && _providers.ContainsKey(c.Provider))
            .ToList();

        logger.LogInformation(
            "[STATUS_SYNC] Starting for {Count} channels, window {From:u}..{To:u}",
            configs.Count, from, to);

        foreach (var config in configs)
        {
            // One unreachable provider must not stop the rest.
            try
            {
                var result = await SyncOneAsync(config, from, to, DefaultLimit);
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
        ChannelConfig config, DateTimeOffset from, DateTimeOffset to, int limit)
    {
        var dto = new StatusSyncResultDto
        {
            ChannelConfigId = config.Id,
            Label = config.Label,
            From = from,
            To = to
        };

        var rows = await recipientRepo.GetPendingForChannelAsync(config.Id, from, to, limit);
        if (rows.Count == 0)
        {
            dto.Success = true;
            return dto;
        }

        var cfg = JsonSerializer.Deserialize<Dictionary<string, string>>(
            enc.Decrypt(config.ConfigJson)) ?? [];

        var result = await _providers[config.Provider].SyncAsync(cfg, rows, from, to);

        dto.Success = result.Success;
        dto.Updated = result.Updated;
        dto.Unmatched = result.Unmatched;
        dto.Error = result.Error;

        // A failed run leaves every row exactly as it was, so nothing is persisted and
        // the next run retries the same window. Only a successful run has changes.
        if (result.Success)
        {
            foreach (var row in rows) recipientRepo.Update(row);
        }

        return dto;
    }

    /// <summary>
    /// The window to sync. Defaults to the previous full day in business time, matching
    /// when the nightly job runs — a UTC day would straddle two local ones and leave a
    /// six-hour gap that nothing ever revisits.
    /// </summary>
    private (DateTimeOffset From, DateTimeOffset To) ResolveWindow(StatusSyncRequest request)
    {
        if (request.From is { } explicitFrom)
            return (explicitFrom, request.To ?? DateTimeOffset.UtcNow);

        var tz = AppTime.Resolve(config);
        var nowLocal = AppTime.ToAppTime(DateTimeOffset.UtcNow, tz);
        var startOfToday = new DateTimeOffset(nowLocal.Date, nowLocal.Offset);

        return (startOfToday.AddDays(-1), request.To ?? startOfToday);
    }
}
