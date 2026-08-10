using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        string number, string message, CancellationToken ct = default) =>
        PostAsync(new
        {
            number,
            type = TextType,
            message,
            instance_id = instanceId,
            access_token = accessToken
        }, number, ct);

    public Task<WaTrendResponse> SendMediaAsync(
        string instanceId, string accessToken,
        string number, string message, string mediaUrl, CancellationToken ct = default) =>
        PostAsync(new
        {
            number,
            type = MediaType,
            message,
            media_url = mediaUrl,
            instance_id = instanceId,
            access_token = accessToken
        }, number, ct);

    private async Task<WaTrendResponse> PostAsync(
        object payload, string number, CancellationToken ct)
    {
        logger.LogInformation("[WATREND] POST {Path} to={Number}", SendPath, number);
        var content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var resp = await http.PostAsync(SendPath, content, ct);
        var body = (await resp.Content.ReadAsStringAsync(ct)).Trim();
        logger.LogDebug("[WATREND] Response for {Number}: {Body}", number, body);

        // watrend returns an empty array `[]` (or empty body) when the number can't be
        // delivered — e.g. it isn't a reachable WhatsApp user. Treat as a clean failure.
        if (string.IsNullOrWhiteSpace(body) || body.StartsWith('['))
        {
            logger.LogWarning(
                "[WATREND] Non-success response for {Number} (number may not be on WhatsApp). Body={Body}",
                number, body);
            return Failure("Number is not reachable on WhatsApp");
        }

        try
        {
            return JsonSerializer.Deserialize<WaTrendResponse>(body, _opts)
                ?? Failure("Provider returned an empty response");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[WATREND] Parse error for {Number}. Body={Body}", number, body);
            // Keep the raw body as the reason — an unrecognised shape is still diagnostic,
            // and discarding it is what made these failures unexplainable.
            return Failure(Truncate(body, 500));
        }
    }

    private static WaTrendResponse Failure(string reason) => new()
    {
        Status = "error",
        Message = new WaTrendMessageDetail { Text = reason }
    };

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}