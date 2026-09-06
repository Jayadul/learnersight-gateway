using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NetZeroGateway.Infrastructure.HttpClients;

public class ChithiClient(
    HttpClient http, IConfiguration config, ILogger<ChithiClient> logger)
    : IChithiClient
{
    private string SendPath => config["Providers:Chithi:SendPath"] ?? "/api/v3/send-sms";
    private string BalancePath => config["Providers:Chithi:BalancePath"] ?? "/api/v3/balance";

    public async Task<ChithiSendResponse> SendAsync(
        string apiToken, string sid,
        string msisdn, string message, string csmsId,
        string? baseUrl = null, CancellationToken ct = default)
    {
        var url = ProviderEndpoint.Resolve(baseUrl, SendPath);
        logger.LogInformation("[CHITHI] POST {Url} to={Msisdn}", url, msisdn);

        var payload = new
        {
            api_token = apiToken,
            sid,
            msisdn,
            sms = message,
            csms_id = csmsId
        };

        // Accept is a request header, not a content header — setting it on the body
        // throws "Misused header name" before the call is ever made.
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var resp = await http.SendAsync(request, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        logger.LogDebug("[CHITHI] Response for {Msisdn}: {Body}", msisdn, body);

        return Parse<ChithiSendResponse>(body)
            ?? new ChithiSendResponse { Status = "ERROR", ErrorMessage = "unreadable_response" };
    }

    public async Task<ChithiBalanceResponse> GetBalanceAsync(
        string apiToken, string? baseUrl = null, CancellationToken ct = default)
    {
        // Chithi takes the token in the query string, where GenNet wants it in a GET
        // body. Same API family, different contract — do not unify these.
        var path = $"{BalancePath}?api_token={Uri.EscapeDataString(apiToken)}";
        var url = ProviderEndpoint.Resolve(baseUrl, path);
        logger.LogInformation("[CHITHI] GET {Path}", BalancePath);

        var resp = await http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        logger.LogDebug("[CHITHI] Balance response: {Body}", body);

        return Parse<ChithiBalanceResponse>(body)
            ?? new ChithiBalanceResponse { Status = "ERROR", ErrorMessage = "unreadable_response" };
    }

    private T? Parse<T>(string body) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[CHITHI] Parse error. Body={Body}", body);
            return null;
        }
    }
}
