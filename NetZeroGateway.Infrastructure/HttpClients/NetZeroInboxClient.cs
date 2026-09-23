using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    private static readonly JsonSerializerOptions TemplatePayloadOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<NetZeroInboxTemplateResponse> SendTemplateAsync(
        string templateToken, string sendTo, string templateName,
        List<string> exampleArr, string? mediaUri,
        string? baseUrl = null, CancellationToken ct = default)
    {
        // A first real test against the vendor (hotfix batch 4) came back with every
        // response field null — the shape matched nothing. Rebuilt to match the vendor's
        // own known-working example exactly: the query-string duplicates of
        // Content-Type/Authorization once assumed to be Postman export noise, and
        // enableLog, which the regular text send already carries but this one omitted.
        var path = $"{TemplatePath}?Content-Type={Uri.EscapeDataString("application/json")}"
                  + $"&Authorization={Uri.EscapeDataString($"Bearer {templateToken}")}";
        var url = ProviderEndpoint.Resolve(baseUrl, path);
        logger.LogInformation(
            "[NZ_INBOX_TEMPLATE] POST {Path} to={To} template={Template}",
            TemplatePath, sendTo, templateName);

        // Keeps the vendor's own "templet" spelling in the wire payload — it is their
        // field name, not ours to correct. mediaUri is omitted entirely when not set,
        // rather than sent as an explicit JSON null, matching the vendor's own example.
        var payload = new
        {
            sendTo,
            templetName = templateName,
            exampleArr,
            token = templateToken,
            mediaUri,
            enableLog = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, TemplatePayloadOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", templateToken);

        var resp = await http.SendAsync(request, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        // Visible at Information, not Debug: the first real test's failure was
        // undiagnosable without this, since Debug never reaches Seq.
        logger.LogInformation(
            "[NZ_INBOX_TEMPLATE] Response for {To}: HTTP {Status} {Body}", sendTo, (int)resp.StatusCode, body);

        if (!resp.IsSuccessStatusCode)
        {
            return new NetZeroInboxTemplateResponse { Error = $"HTTP {(int)resp.StatusCode}: {body}" };
        }

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
