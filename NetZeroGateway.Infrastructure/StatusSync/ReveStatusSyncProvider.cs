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

            // A rejected lookup is not a failed delivery (hotfix batch 9, Task 4) — REVE
            // returns REJECTD for a bad/already-answered/expired message id just as
            // readily as for any other query-level problem, none of which say the SMS
            // failed to arrive. Same treatment Chithi's aged-out ids already get.
            var state = r.IsLookupRejected ? DeliveryStatus.Unknown : MapState(r.Text);
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
    /// Only called once <see cref="ReveStatusResponse.IsLookupRejected"/> is already
    /// ruled out, so REJECTD never reaches here — DELIVRD is the only success, the
    /// remaining recognised terminal words are real delivery failures, and anything
    /// unrecognised stays Unknown rather than being guessed into Failed. The raw payload
    /// is stored either way so a new state is visible for review.
    /// <para>
    /// The in-flight states matter as much as the terminal ones. REVE answers SENT while
    /// a message is with the carrier, and mapping that to Unknown strands it: the sync
    /// only picks up Pending rows, so a message that would have resolved to DELIVRD on
    /// the next run never gets asked about again. They stay Pending so it does.
    /// </para>
    /// </summary>
    private static DeliveryStatus MapState(string? text) => text?.Trim().ToUpperInvariant() switch
    {
        "DELIVRD" => DeliveryStatus.Delivered,

        // Terminal failures actually about the SMS itself, not the status query.
        "EXPIRED" or "UNDELIV" or "DELETED" => DeliveryStatus.Failed,

        // Still on its way — ask again next run rather than settling on an answer.
        "SENT" or "ACCEPTD" or "ENROUTE" or "PENDING" => DeliveryStatus.Pending,

        null or "" => DeliveryStatus.Unknown,
        _ => DeliveryStatus.Unknown
    };
}
