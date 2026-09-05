using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using System.Text;
using System.Text.Json;

namespace NetZeroGateway.Infrastructure.HttpClients;

public class GenNetClient(
    HttpClient http, IConfiguration config, ILogger<GenNetClient> logger)
    : IGenNetClient
{
    private string SendPath => config["Providers:GenNet:SendPath"] ?? "/api/v3/send-sms/bulk";
    private string BalancePath => config["Providers:GenNet:BalancePath"] ?? "/api/v3/balance";

    public async Task<GenNetSendResponse> SendBulkAsync(
        string apiToken, string sid,
        List<string> msisdns, string message, string batchCsmsId,
        string? baseUrl = null, CancellationToken ct = default)
    {
        var url = ProviderEndpoint.Resolve(baseUrl, SendPath);
        logger.LogInformation(
            "[GENNET] POST {Url} count={Count} batch={Batch}", url, msisdns.Count, batchCsmsId);

        var payload = new
        {
            api_token = apiToken,
            sid,
            msisdn = msisdns,
            sms = message,
            batch_csms_id = batchCsmsId
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var resp = await http.PostAsync(url, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        logger.LogDebug("[GENNET] Response: {Body}", body);

        return Parse<GenNetSendResponse>(body)
            ?? new GenNetSendResponse { Status = "ERROR", ErrorMessage = "unreadable_response" };
    }

    public async Task<GenNetBalanceResponse> GetBalanceAsync(
        string apiToken, string? baseUrl = null, CancellationToken ct = default)
    {
        var url = ProviderEndpoint.Resolve(baseUrl, BalancePath);
        logger.LogInformation("[GENNET] GET {Url}", url);

        // A GET carrying a JSON body is unusual, but it is the documented contract.
        // HttpRequestMessage allows it where the GetAsync overloads do not.
        using var request = new HttpRequestMessage(HttpMethod.Get, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { api_token = apiToken }),
                Encoding.UTF8, "application/json")
        };

        var resp = await http.SendAsync(request, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        logger.LogDebug("[GENNET] Balance response: {Body}", body);

        return Parse<GenNetBalanceResponse>(body)
            ?? new GenNetBalanceResponse { Status = "ERROR", ErrorMessage = "unreadable_response" };
    }

    /// <summary>
    /// Deserializes, or returns null when the body is not the shape we expect. The
    /// caller turns that into a readable failure — a provider changing its error shape
    /// must not surface as a parse exception.
    /// </summary>
    private T? Parse<T>(string body) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[GENNET] Parse error. Body={Body}", body);
            return null;
        }
    }
}
