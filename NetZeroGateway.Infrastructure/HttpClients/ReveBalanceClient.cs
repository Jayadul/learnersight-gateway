using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using System.Text.Json;

namespace NetZeroGateway.Infrastructure.HttpClients;

public class ReveBalanceClient(
    HttpClient http, IConfiguration config, ILogger<ReveBalanceClient> logger)
    : IReveBalanceClient
{
    private string BalancePath => config["Providers:Sms:BalancePath"]
        ?? "/sms/smsConfiguration/smsClientBalance.jsp";

    public async Task<ReveBalanceResponse> GetBalanceAsync(
        string clientId, string sessionCookie,
        string? baseUrl = null, CancellationToken ct = default)
    {
        var path = $"{BalancePath}?client={Uri.EscapeDataString(clientId)}";
        var url = ProviderEndpoint.Resolve(baseUrl, path);
        logger.LogInformation("[REVE_BALANCE] GET {Path} client={Client}", BalancePath, clientId);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        // Set per request rather than on the client: the cookie belongs to one channel's
        // config, and a shared HttpClient serves every REVE channel.
        request.Headers.Add("Cookie", $"JSESSIONID={sessionCookie}");

        var resp = await http.SendAsync(request, ct);
        var body = (await resp.Content.ReadAsStringAsync(ct)).Trim();
        logger.LogDebug("[REVE_BALANCE] Response: {Body}", body);

        // An expired session gets an HTML login page rather than JSON. Say so plainly —
        // the admin's fix is to paste a fresh cookie, and "unexpected character '<'"
        // would not tell them that.
        if (body.StartsWith('<'))
        {
            logger.LogWarning(
                "[REVE_BALANCE] Non-JSON response — the session cookie has most likely expired.");
            return new ReveBalanceResponse
            {
                Error = "The REVE session cookie has expired. Paste a fresh JSESSIONID "
                      + "into this channel's Balance Session Cookie field."
            };
        }

        try
        {
            return JsonSerializer.Deserialize<ReveBalanceResponse>(body)
                ?? new ReveBalanceResponse { Error = "unreadable_response" };
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[REVE_BALANCE] Parse error. Body={Body}", body);
            return new ReveBalanceResponse { Error = ex.Message };
        }
    }
}
