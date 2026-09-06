using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using System.Text.Json;
using System.Web;

namespace NetZeroGateway.Infrastructure.HttpClients;

public class ChithiStatusClient(
    HttpClient http, IConfiguration config, ILogger<ChithiStatusClient> logger)
    : IChithiStatusClient
{
    private string StatusPath =>
        config["Providers:Chithi:StatusPath"] ?? "/api/v3/send-sms/status";

    public async Task<ChithiStatusResponse> GetStatusAsync(
        string apiToken, string referenceId,
        string? baseUrl = null, CancellationToken ct = default)
    {
        var q = HttpUtility.ParseQueryString(string.Empty);
        q["api_token"] = apiToken;
        q["reference_id"] = referenceId;

        var url = ProviderEndpoint.Resolve(baseUrl, $"{StatusPath}?{q}");
        // The token is in the query string, so only the path is logged.
        logger.LogDebug("[CHITHI_STATUS] GET {Path} ref={Ref}", StatusPath, referenceId);

        var resp = await http.GetAsync(url, ct);
        var body = (await resp.Content.ReadAsStringAsync(ct)).Trim();

        if (string.IsNullOrWhiteSpace(body))
            return new ChithiStatusResponse { Error = "empty_response" };

        try
        {
            return JsonSerializer.Deserialize<ChithiStatusResponse>(body)
                ?? new ChithiStatusResponse { Error = "unreadable_response" };
        }
        catch (JsonException ex)
        {
            logger.LogError(
                ex, "[CHITHI_STATUS] Parse error for {Ref}. Body={Body}", referenceId, body);
            return new ChithiStatusResponse { Error = ex.Message };
        }
    }
}
