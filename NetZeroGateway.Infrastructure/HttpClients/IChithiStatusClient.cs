using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public interface IChithiStatusClient
{
    /// <summary>
    /// Looks up one reference id. Chithi returns one id per recipient, so this is a
    /// clean 1:1 lookup — unlike REVE, whose id covers a whole batch.
    /// </summary>
    Task<ChithiStatusResponse> GetStatusAsync(
        string apiToken, string referenceId,
        string? baseUrl = null,
        CancellationToken ct = default);
}
