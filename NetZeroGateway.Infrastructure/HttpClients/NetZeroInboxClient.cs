using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using System.Text;
using System.Text.Json;

namespace NetZeroGateway.Infrastructure.HttpClients;

public class NetZeroInboxClient(
    HttpClient http, IConfiguration config, ILogger<NetZeroInboxClient> logger)
    : INetZeroInboxClient
{
    private string SendPath => config["Providers:NetZeroInbox:SendPath"] ?? "/api/v1/send-message";

    public async Task<NetZeroInboxResponse> SendTextAsync(
        string token, string to, string message,
        string? baseUrl = null, CancellationToken ct = default)
    {
        // The token is a credential and it travels in the query string, so the request
        // URL is never logged — only the path. Every other provider takes its
        // credentials in the body, where they were never at risk of being logged.
        var path = $"{SendPath}?token={Uri.EscapeDataString(token)}";
        var url = ProviderEndpoint.Resolve(baseUrl, path);
        logger.LogInformation("[NZ_INBOX] POST {Path} to={To}", SendPath, to);

        var payload = new
        {
            messageObject = new
            {
                messaging_product = "whatsapp",
                to,
                type = "text",
                text = new { body = message }
            },
            enableLog = true
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var resp = await http.PostAsync(url, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        logger.LogDebug("[NZ_INBOX] Response for {To}: {Body}", to, body);

        try
        {
            return JsonSerializer.Deserialize<NetZeroInboxResponse>(body)
                ?? new NetZeroInboxResponse { Error = "unreadable_response" };
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[NZ_INBOX] Parse error for {To}. Body={Body}", to, body);
            return new NetZeroInboxResponse { Error = ex.Message };
        }
    }
}
