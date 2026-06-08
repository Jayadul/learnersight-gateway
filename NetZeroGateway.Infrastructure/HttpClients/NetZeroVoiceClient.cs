using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public class NetZeroVoiceClient(HttpClient http, ILogger<NetZeroVoiceClient> logger)
    : INetZeroVoiceClient
{
    public async Task<NetZeroVoiceResponse> RunCampaignAsync(
        string owner, string password, string authUserId,
        string campaignId, string lotNumber,
        List<string> destinationNumbers, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[VOICE] POST /api/v1/run-campaign campaign={Campaign} count={Count}",
            campaignId, destinationNumbers.Count);

        var payload = new
        {
            actionId           = "runCampaign",
            owner,
            password,
            campaignId,
            authUserId,
            lotNumber,
            service            = "campaign",
            destinationNumbers
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var resp = await http.PostAsync("/api/v1/run-campaign", content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        logger.LogDebug("[VOICE] Raw response: {Body}", body);

        try
        {
            return JsonSerializer.Deserialize<NetZeroVoiceResponse>(body)
                ?? new NetZeroVoiceResponse { Status = "error", Message = "null_response" };
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[VOICE] JSON parse error. Body={Body}", body);
            return new NetZeroVoiceResponse { Status = "error", Message = ex.Message };
        }
    }
}
