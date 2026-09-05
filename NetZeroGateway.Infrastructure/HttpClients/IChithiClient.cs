using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public interface IChithiClient
{
    /// <summary>
    /// Sends to a single recipient. Chithi takes one <c>msisdn</c> per call, unlike
    /// GenNet's array — the two share a vendor platform and credentials but not a
    /// request shape.
    /// </summary>
    Task<ChithiSendResponse> SendAsync(
        string apiToken, string sid,
        string msisdn, string message, string csmsId,
        string? baseUrl = null,
        CancellationToken ct = default);

    Task<ChithiBalanceResponse> GetBalanceAsync(
        string apiToken, string? baseUrl = null, CancellationToken ct = default);
}
