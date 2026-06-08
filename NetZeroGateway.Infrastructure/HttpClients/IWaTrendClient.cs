using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public interface IWaTrendClient
{
    Task<WaTrendResponse> SendTextAsync(
        string instanceId, string accessToken,
        string number, string message,
        CancellationToken ct = default);

    Task<WaTrendResponse> SendMediaAsync(
        string instanceId, string accessToken,
        string number, string message, string mediaUrl,
        CancellationToken ct = default);
}
