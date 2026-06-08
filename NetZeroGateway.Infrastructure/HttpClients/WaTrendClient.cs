using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public class WaTrendClient(HttpClient http, ILogger<WaTrendClient> logger) : IWaTrendClient
{
    private static readonly JsonSerializerOptions _opts = new() { PropertyNamingPolicy = null };

    public Task<WaTrendResponse> SendTextAsync(
        string instanceId, string accessToken,
        string number, string message, CancellationToken ct = default)
    {
        var payload = new
        {
            number,
            type         = "text",
            message,
            instance_id  = instanceId,
            access_token = accessToken
        };
        return PostAsync(payload, number, ct);
    }

    public Task<WaTrendResponse> SendMediaAsync(
        string instanceId, string accessToken,
        string number, string message, string mediaUrl, CancellationToken ct = default)
    {
        var payload = new
        {
            number,
            type         = "media",
            message,
            media_url    = mediaUrl,
            instance_id  = instanceId,
            access_token = accessToken
        };
        return PostAsync(payload, number, ct);
    }

    private async Task<WaTrendResponse> PostAsync(object payload, string number, CancellationToken ct)
    {
        logger.LogInformation("[WATREND] POST /api/send to={Number}", number);

        var content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var resp = await http.PostAsync("/api/send", content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        logger.LogDebug("[WATREND] Raw response for {Number}: {Body}", number, body);

        try
        {
            return JsonSerializer.Deserialize<WaTrendResponse>(body, _opts)
                ?? new WaTrendResponse { Status = "error" };
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[WATREND] JSON parse error for {Number}. Body={Body}", number, body);
            return new WaTrendResponse { Status = "error" };
        }
    }
}
