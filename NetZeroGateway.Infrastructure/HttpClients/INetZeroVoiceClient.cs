using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public interface INetZeroVoiceClient
{
    Task<NetZeroVoiceResponse> RunCampaignAsync(
        string owner, string password, string authUserId,
        string campaignId, string lotNumber,
        List<string> destinationNumbers,
        CancellationToken ct = default);
}
