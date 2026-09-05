using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public interface IGenNetClient
{
    /// <summary>
    /// Sends one message to many recipients in a single call. GenNet is genuinely bulk,
    /// which matches the gateway's "SMS is one job, recipients comma-joined" semantics.
    /// Numbers must already be in GenNet's local form — see RecipientFormat.
    /// </summary>
    Task<GenNetSendResponse> SendBulkAsync(
        string apiToken, string sid,
        List<string> msisdns, string message, string batchCsmsId,
        string? baseUrl = null,
        CancellationToken ct = default);

    Task<GenNetBalanceResponse> GetBalanceAsync(
        string apiToken, string? baseUrl = null, CancellationToken ct = default);
}
