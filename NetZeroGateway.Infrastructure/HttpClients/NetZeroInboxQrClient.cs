using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using System.Text.Json;
using System.Web;

namespace NetZeroGateway.Infrastructure.HttpClients;

public class NetZeroInboxQrClient(
    HttpClient http, IConfiguration config, ILogger<NetZeroInboxQrClient> logger)
    : INetZeroInboxQrClient
{
    private string SendPath =>
        config["Providers:NetZeroInboxQr:SendPath"] ?? "/api/qr/rest/send_message";

    public Task<NetZeroInboxQrResponse> SendTextAsync(
        string token, string from, string to, string message,
        string? baseUrl = null, CancellationToken ct = default)
    {
        // Everything is a query parameter here, including the token and the message
        // body. HttpUtility builds the string so a message containing & or = cannot
        // break out and forge another parameter.
        var q = HttpUtility.ParseQueryString(string.Empty);
        q["messageType"] = "text";
        q["requestType"] = "GET";
        q["token"] = token;
        q["from"] = from;
        q["to"] = to;
        q["text"] = message;

        return ExecuteAsync(q, from, to, ct, baseUrl);
    }

    public Task<NetZeroInboxQrResponse> SendImageAsync(
        string token, string from, string to, string imageUrl, string caption,
        string? baseUrl = null, CancellationToken ct = default)
    {
        var q = HttpUtility.ParseQueryString(string.Empty);
        q["messageType"] = "image";
        q["requestType"] = "GET";
        q["token"] = token;
        q["from"] = from;
        q["to"] = to;
        q["imageUrl"] = imageUrl;
        q["caption"] = caption;

        return ExecuteAsync(q, from, to, ct, baseUrl);
    }

    private async Task<NetZeroInboxQrResponse> ExecuteAsync(
        System.Collections.Specialized.NameValueCollection q, string from, string to,
        CancellationToken ct, string? baseUrl = null)
    {
        var url = ProviderEndpoint.Resolve(baseUrl, $"{SendPath}?{q}");

        // The token is in the URL, so the URL is never logged — only the path.
        logger.LogInformation(
            "[NZ_INBOX_QR] GET {Path} ({MessageType}) from={From} to={To}",
            SendPath, q["messageType"], from, to);

        var resp = await http.GetAsync(url, ct);
        var body = (await resp.Content.ReadAsStringAsync(ct)).Trim();
        logger.LogDebug("[NZ_INBOX_QR] Response for {To}: {Body}", to, body);

        if (string.IsNullOrWhiteSpace(body))
            return new NetZeroInboxQrResponse { Error = "empty_response" };

        try
        {
            return JsonSerializer.Deserialize<NetZeroInboxQrResponse>(body)
                ?? new NetZeroInboxQrResponse { Error = "unreadable_response" };
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[NZ_INBOX_QR] Parse error for {To}. Body={Body}", to, body);
            return new NetZeroInboxQrResponse { Error = ex.Message };
        }
    }
}
