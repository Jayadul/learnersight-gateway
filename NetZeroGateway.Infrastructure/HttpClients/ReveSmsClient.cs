using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using System.Text.Json;

namespace NetZeroGateway.Infrastructure.HttpClients;

public class ReveSmsClient(
    HttpClient http, IConfiguration config, ILogger<ReveSmsClient> logger)
    : IReveSmsClient
{
    private string SendPath => config["Providers:Sms:SendPath"] ?? "/sendtext";

    public async Task<ReveSmsResponse> SendAsync(
        string apiKey, string secretKey, string callerID,
        string toUser, string message,
        string? baseUrl = null, CancellationToken ct = default)
    {
        // Absolute when the channel names its own host, else the bare path against the
        // registered BaseAddress — two REVE channels may point at different hosts.
        var url = ProviderEndpoint.Resolve(baseUrl, SendPath);
        logger.LogInformation("[REVE_SMS] POST {Url} to={To}", url, toUser);
        var form = new FormUrlEncodedContent([
            new("apikey",         apiKey),
            new("secretkey",      secretKey),
            new("callerID",       callerID),
            new("toUser",         toUser),
            new("messageContent", message)
        ]);
        var resp = await http.PostAsync(url, form, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        logger.LogDebug("[REVE_SMS] Response: {Body}", body);
        try
        {
            return JsonSerializer.Deserialize<ReveSmsResponse>(body)
                ?? new ReveSmsResponse { Status = "ERR", Text = "null_response" };
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[REVE_SMS] Parse error. Body={Body}", body);
            return new ReveSmsResponse { Status = "ERR", Text = ex.Message };
        }
    }
}