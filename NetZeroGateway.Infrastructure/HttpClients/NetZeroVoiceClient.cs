using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using System.Text;
using System.Text.Json;

namespace NetZeroGateway.Infrastructure.HttpClients;

public class NetZeroVoiceClient(
    HttpClient http, IConfiguration config, ILogger<NetZeroVoiceClient> logger)
    : INetZeroVoiceClient
{
    private string CampaignPath => config["Providers:Voice:CampaignPath"] ?? "/api/v1/run-campaign";
    private string ActionId => config["Providers:Voice:ActionId"] ?? "runCampaign";
    private string Service => config["Providers:Voice:Service"] ?? "campaign";

    public async Task<NetZeroVoiceResponse> RunCampaignAsync(
        string owner, string password, string authUserId,
        string campaignId, string lotNumber,
        List<string> destinationNumbers,
        string? baseUrl = null, CancellationToken ct = default)
    {
        var url = ProviderEndpoint.Resolve(baseUrl, CampaignPath);
        logger.LogInformation(
            "[VOICE] POST {Url} campaign={Campaign} count={Count}",
            url, campaignId, destinationNumbers.Count);

        var payload = new
        {
            actionId = ActionId,
            owner,
            password,
            campaignId,
            authUserId,
            lotNumber,
            service = Service,
            destinationNumbers
        };
        var content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var resp = await http.PostAsync(url, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        logger.LogDebug("[VOICE] Response: {Body}", body);
        try
        {
            return JsonSerializer.Deserialize<NetZeroVoiceResponse>(body)
                ?? new NetZeroVoiceResponse { Status = "error", Message = "null_response" };
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[VOICE] Parse error. Body={Body}", body);
            return new NetZeroVoiceResponse { Status = "error", Message = ex.Message };
        }
    }
}