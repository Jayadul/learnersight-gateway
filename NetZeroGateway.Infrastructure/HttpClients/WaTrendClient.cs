using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using System.Text;
using System.Text.Json;

namespace NetZeroGateway.Infrastructure.HttpClients;

public class WaTrendClient(
    HttpClient http, IConfiguration config, ILogger<WaTrendClient> logger)
    : IWaTrendClient
{
    private string SendPath => config["Providers:WhatsApp:SendPath"] ?? "/api/send";
    private string TextType => config["Providers:WhatsApp:TextType"] ?? "text";
    private string MediaType => config["Providers:WhatsApp:MediaType"] ?? "media";

    private static readonly JsonSerializerOptions _opts = new() { PropertyNamingPolicy = null };

    public Task<WaTrendResponse> SendTextAsync(
        string instanceId, string accessToken,
        string number, string message,
        string? baseUrl = null, CancellationToken ct = default) =>
        PostAsync(new
        {
            number,
            type = TextType,
            message,
            instance_id = instanceId,
            access_token = accessToken
        }, number, baseUrl, ct);

    public Task<WaTrendResponse> SendMediaAsync(
        string instanceId, string accessToken,
        string number, string message, string mediaUrl,
        string? baseUrl = null, CancellationToken ct = default) =>
        PostAsync(new
        {
            number,
            type = MediaType,
            message,
            media_url = mediaUrl,
            instance_id = instanceId,
            access_token = accessToken
        }, number, baseUrl, ct);

    private async Task<WaTrendResponse> PostAsync(
        object payload, string number, string? baseUrl, CancellationToken ct)
    {
        var url = ProviderEndpoint.Resolve(baseUrl, SendPath);
        logger.LogInformation("[WATREND] POST {Url} to={Number}", url, number);
        var content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var resp = await http.PostAsync(url, content, ct);
        var body = (await resp.Content.ReadAsStringAsync(ct)).Trim();
        logger.LogDebug("[WATREND] Response for {Number}: {Body}", number, body);

        // watrend returns an empty array `[]` (or empty body) when the number can't be
        // delivered — e.g. it isn't a reachable WhatsApp user. Treat as a clean failure.
        if (string.IsNullOrWhiteSpace(body) || body.StartsWith('['))
        {
            logger.LogWarning(
                "[WATREND] Non-success response for {Number} (number may not be on WhatsApp). Body={Body}",
                number, body);
            return new WaTrendResponse { Status = "error" };
        }

        try
        {
            return JsonSerializer.Deserialize<WaTrendResponse>(body, _opts)
                ?? new WaTrendResponse { Status = "error" };
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[WATREND] Parse error for {Number}. Body={Body}", number, body);
            return new WaTrendResponse { Status = "error" };
        }
    }
}