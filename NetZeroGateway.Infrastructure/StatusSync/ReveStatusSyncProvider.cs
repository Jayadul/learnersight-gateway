using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;

namespace NetZeroGateway.Infrastructure.StatusSync;

public class ReveStatusSyncProvider(
    IReveStatusClient client,
    ILogger<ReveStatusSyncProvider> logger
) : IChannelStatusSyncProvider
{
    public Provider Provider => Provider.Reve;

    public async Task<StatusSyncResult> SyncAsync(
        Dictionary<string, string> cfg,
        List<DispatchJobRecipient> rows,
        DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default)
    {
        if (!cfg.TryGetValue("apikey", out var apiKey) || !cfg.TryGetValue("secretkey", out var secret))
            return StatusSyncResult.Failed("This channel has no REVE credentials configured.");

        var baseUrl = ProviderEndpoint.BaseUrlOf(cfg);
        var updated = 0;
        var unmatched = 0;

        // REVE returns one id per batched job, so every recipient of a job shares it.
        // Looking it up once per job rather than once per row turns a 500-recipient
        // batch into one call instead of 500 identical ones.
        foreach (var group in rows.Where(r => r.ProviderReferenceId is not null)
                                  .GroupBy(r => r.ProviderReferenceId!))
        {
            var r = await client.GetStatusAsync(apiKey, secret, group.Key, baseUrl, ct);

            if (r.Error is not null)
            {
                // One bad lookup is not a failed sync — the rest of the batch may answer.
                logger.LogWarning(
                    "[REVE_STATUS] Lookup failed for {Ref}: {Error}", group.Key, r.Error);
                unmatched += group.Count();
                continue;
            }

            var state = MapState(r.Text);
            var raw = JsonSerializer.Serialize(r);

            foreach (var row in group)
            {
                row.DeliveryStatus = state;
                row.RawStatusResponse = raw;
                row.LastCheckedAt = DateTimeOffset.UtcNow;
                updated++;
            }
        }

        unmatched += rows.Count(r => r.ProviderReferenceId is null);
        return StatusSyncResult.Ok(updated, unmatched);
    }

    /// <summary>
    /// DELIVRD is the only success. Everything else recognised is a failure, and an
    /// unrecognised state stays Unknown rather than being guessed into Failed — the raw
    /// payload is stored either way so a new state is visible for review.
    /// </summary>
    private static DeliveryStatus MapState(string? text) => text?.Trim().ToUpperInvariant() switch
    {
        "DELIVRD" => DeliveryStatus.Delivered,
        "REJECTD" or "EXPIRED" or "UNDELIV" or "DELETED" => DeliveryStatus.Failed,
        null or "" => DeliveryStatus.Unknown,
        _ => DeliveryStatus.Unknown
    };
}
