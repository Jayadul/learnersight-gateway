using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;

namespace NetZeroGateway.Infrastructure.StatusSync;

public class ChithiStatusSyncProvider(
    IChithiStatusClient client,
    ILogger<ChithiStatusSyncProvider> logger
) : IChannelStatusSyncProvider
{
    public Provider Provider => Provider.Chithi;

    public async Task<StatusSyncResult> SyncAsync(
        Dictionary<string, string> cfg,
        List<DispatchJobRecipient> rows,
        DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default)
    {
        if (!cfg.TryGetValue("api_token", out var token) || string.IsNullOrWhiteSpace(token))
            return StatusSyncResult.Failed("This channel has no API token configured.");

        var baseUrl = ProviderEndpoint.BaseUrlOf(cfg);
        var updated = 0;
        var unmatched = 0;

        // One reference id per recipient — a clean 1:1 lookup, unlike REVE's batch id.
        foreach (var row in rows)
        {
            if (row.ProviderReferenceId is not { } reference)
            {
                unmatched++;
                continue;
            }

            var r = await client.GetStatusAsync(token, reference, baseUrl, ct);

            if (r.Error is not null)
            {
                logger.LogWarning(
                    "[CHITHI_STATUS] Lookup failed for {Ref}: {Error}", reference, r.Error);
                unmatched++;
                continue;
            }

            // A rejected lookup is not a failed delivery. Chithi ages reference ids out,
            // so this is expected for old rows — mark Unknown and stop asking rather
            // than recording a delivery failure that never happened.
            if (r.IsLookupRejected)
            {
                row.DeliveryStatus = DeliveryStatus.Unknown;
                row.RawStatusResponse = JsonSerializer.Serialize(r);
                row.LastCheckedAt = DateTimeOffset.UtcNow;
                updated++;
                continue;
            }

            row.DeliveryStatus = MapState(r.DeliveryState);
            row.RawStatusResponse = JsonSerializer.Serialize(r);
            row.LastCheckedAt = DateTimeOffset.UtcNow;
            updated++;
        }

        return StatusSyncResult.Ok(updated, unmatched);
    }

    private static DeliveryStatus MapState(string? state) => state?.Trim().ToLowerInvariant() switch
    {
        "success" or "delivered" => DeliveryStatus.Delivered,
        "failed" or "rejected" or "expired" or "undelivered" => DeliveryStatus.Failed,
        null or "" => DeliveryStatus.Unknown,
        // Still in flight at the provider, or a state we have not catalogued. Either
        // way, not a failure.
        _ => DeliveryStatus.Unknown
    };
}
