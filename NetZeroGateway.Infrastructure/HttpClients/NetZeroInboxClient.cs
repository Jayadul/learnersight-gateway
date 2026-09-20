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
    private string TemplatePath => config["Providers:NetZeroInbox:TemplatePath"] ?? "/api/v1/send_templet";

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

    public async Task<NetZeroInboxTemplateResponse> SendTemplateAsync(
        string templateToken, string sendTo, string templateName,
        List<string> exampleArr, string? mediaUri,
        string? baseUrl = null, CancellationToken ct = default)
    {
        var url = ProviderEndpoint.Resolve(baseUrl, TemplatePath);
        logger.LogInformation(
            "[NZ_INBOX_TEMPLATE] POST {Path} to={To} template={Template}",
            TemplatePath, sendTo, templateName);

        // Keeps the vendor's own "templet" spelling in the wire payload — it is their
        // field name, not ours to correct. The query-string duplicates of
        // Content-Type/Authorization shown in the vendor's example curl look like
        // leftover Postman export noise rather than a real requirement, so this sends
        // header + JSON body only; add them only if a real test against the endpoint
        // proves they are actually needed.
        var payload = new
        {
            sendTo,
            templetName = templateName,
            exampleArr,
            token = templateToken,
            mediaUri
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", templateToken);

        var resp = await http.SendAsync(request, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        logger.LogDebug("[NZ_INBOX_TEMPLATE] Response for {To}: {Body}", sendTo, body);

        try
        {
            return JsonSerializer.Deserialize<NetZeroInboxTemplateResponse>(body)
                ?? new NetZeroInboxTemplateResponse { Error = "unreadable_response" };
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[NZ_INBOX_TEMPLATE] Parse error for {To}. Body={Body}", sendTo, body);
            return new NetZeroInboxTemplateResponse { Error = ex.Message };
        }
    }
}
