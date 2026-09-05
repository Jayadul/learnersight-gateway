using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using System.Text.Json;

namespace NetZeroGateway.Infrastructure.HttpClients;

public class ReveStatusClient(
    HttpClient http, IConfiguration config, ILogger<ReveStatusClient> logger)
    : IReveStatusClient
{
    private string StatusPath => config["Providers:Sms:StatusPath"] ?? "/getstatus";

    public async Task<ReveStatusResponse> GetStatusAsync(
        string apiKey, string secretKey, string messageId,
        string? baseUrl = null, CancellationToken ct = default)
    {
        var url = ProviderEndpoint.Resolve(baseUrl, StatusPath);
        logger.LogDebug("[REVE_STATUS] POST {Url} messageid={MsgId}", url, messageId);

        var form = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("apikey", apiKey),
            new KeyValuePair<string, string>("secretkey", secretKey),
            new KeyValuePair<string, string>("messageid", messageId)
        ]);

        var resp = await http.PostAsync(url, form, ct);
        var body = (await resp.Content.ReadAsStringAsync(ct)).Trim();

        if (string.IsNullOrWhiteSpace(body))
            return new ReveStatusResponse { Error = "empty_response" };

        try
        {
            return JsonSerializer.Deserialize<ReveStatusResponse>(body)
                ?? new ReveStatusResponse { Error = "unreadable_response" };
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[REVE_STATUS] Parse error for {MsgId}. Body={Body}", messageId, body);
            return new ReveStatusResponse { Error = ex.Message };
        }
    }
}
