using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public interface IReveSmsClient
{
    Task<ReveSmsResponse> SendAsync(
        string apiKey, string secretKey, string callerID,
        string toUser, string message,
        CancellationToken ct = default);
}
