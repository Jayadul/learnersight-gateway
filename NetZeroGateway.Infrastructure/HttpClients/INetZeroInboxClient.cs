using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public interface INetZeroInboxClient
{
    /// <summary>
    /// Sends one WhatsApp text message. The recipient must already carry a leading plus
    /// — see RecipientFormat. Media is not implemented: the vendor has not documented a
    /// type value for it, and guessing a request shape is worse than a clear rejection.
    /// </summary>
    Task<NetZeroInboxResponse> SendTextAsync(
        string token, string to, string message,
        string? baseUrl = null,
        CancellationToken ct = default);
}
