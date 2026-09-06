using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public interface IReveStatusClient
{
    /// <summary>
    /// Looks up one message id. REVE returns a single id per batched job, so every
    /// recipient of that job resolves to the same answer — a REVE limitation, not a
    /// modelling gap. Uses the channel's send credentials and host; no new fields.
    /// </summary>
    Task<ReveStatusResponse> GetStatusAsync(
        string apiKey, string secretKey, string messageId,
        string? baseUrl = null,
        CancellationToken ct = default);
}
