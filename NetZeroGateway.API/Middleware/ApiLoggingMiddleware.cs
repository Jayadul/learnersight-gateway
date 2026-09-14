using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;
using System.Diagnostics;
using System.Text;

namespace NetZeroGateway.API.Middleware;

/// <summary>
/// Logs all POST and GET /sendtext requests and responses to the ApiLogs table.
/// Captures the request payload — form body for POST, query string for GET — and the
/// response body without breaking the pipeline.
/// </summary>
public class ApiLoggingMiddleware(RequestDelegate next, ILogger<ApiLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var config = ctx.RequestServices.GetRequiredService<IConfiguration>();
        var endpointPath = config[ConfigKeys.GatewayEndpointPath] ?? "/sendtext";
        var isGet = ctx.Request.Method == "GET";

        if ((!isGet && ctx.Request.Method != "POST") ||
            !ctx.Request.Path.Equals(endpointPath, StringComparison.OrdinalIgnoreCase))
        {
            await next(ctx); return;
        }

        string reqBody;
        if (isGet)
        {
            // Credentials arrive in the query string here, not a form body — mask the
            // same two fields there. A URL shows up in more places than a POST body
            // (access logs, browser history, proxies), so this matters even more on GET.
            reqBody = MaskSecrets(ctx.Request.QueryString.Value ?? "");
        }
        else
        {
            ctx.Request.EnableBuffering();
            reqBody = MaskSecrets(await ReadBodyAsync(ctx.Request));
            ctx.Request.Body.Position = 0;
        }

        var originalBody = ctx.Response.Body;
        using var capture = new MemoryStream();
        ctx.Response.Body = capture;

        var sw = Stopwatch.StartNew();
        await next(ctx);
        sw.Stop();

        capture.Position = 0;
        var respBody = await new StreamReader(capture).ReadToEndAsync();
        capture.Position = 0;
        await capture.CopyToAsync(originalBody);
        ctx.Response.Body = originalBody;

        string? toUserVal = null;
        string? requestedChannels = null;
        try
        {
            if (isGet)
            {
                toUserVal = ctx.Request.Query["toUser"].FirstOrDefault();
                requestedChannels = DescribeChannels(
                    ctx.RequestServices, ctx.Request.Query["messageContent"].FirstOrDefault());
            }
            else if (ctx.Request.HasFormContentType)
            {
                ctx.Request.Body.Position = 0;
                var form = await ctx.Request.ReadFormAsync();
                ctx.Request.Body.Position = 0;
                toUserVal = form["toUser"].FirstOrDefault();
                requestedChannels = DescribeChannels(
                    ctx.RequestServices, form["messageContent"].FirstOrDefault());
            }
        }
        catch { /* never break pipeline */ }

        var messageRequestId = ExtractMessageId(respBody);

        try
        {
            await using var scope = ctx.RequestServices.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IApiLogRepository>();
            await repo.AddAsync(new ApiLog
            {
                ToUser = toUserVal,
                RawBody = reqBody,
                ResponseBody = respBody,
                CallerIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "",
                ResponseStatus = ctx.Response.StatusCode,
                DurationMs = sw.ElapsedMilliseconds,
                RequestedAt = DateTimeOffset.UtcNow,
                MessageRequestId = messageRequestId,
                RequestedChannels = requestedChannels
            });
            await repo.SaveChangesAsync();
            logger.LogInformation("[API_LOG] {Status} {Ms}ms | To={To} | IP={Ip}",
                ctx.Response.StatusCode, sw.ElapsedMilliseconds,
                toUserVal, ctx.Connection.RemoteIpAddress);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[API_LOG] Failed to persist log record");
        }
    }

    /// <summary>
    /// The channel types the tags asked for, comma-joined and de-duplicated. Parsing here
    /// costs one extra pass over a short string and keeps the log truthful about what was
    /// requested — dispatch runs in the background, so the gateway that eventually
    /// delivered is not knowable at this point. A rejected message has no channels.
    /// </summary>
    private static string? DescribeChannels(IServiceProvider services, string? messageContent)
    {
        if (string.IsNullOrWhiteSpace(messageContent)) return null;

        var parser = services.GetRequiredService<ITemplateParserService>();
        var parsed = parser.Parse(messageContent);
        if (!parsed.IsValid || parsed.Jobs.Count == 0) return null;

        var names = parsed.Jobs
            .Select(j => j.ChannelType.ToString())
            .Distinct()
            .ToList();

        return string.Join(", ", names);
    }

    /// <summary>
    /// Pulls Message_ID out of the response we just captured. The column existed but was
    /// never populated, so an API log could not be traced to the deliveries it produced.
    /// </summary>
    private static Guid? ExtractMessageId(string responseBody)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
            return doc.RootElement.TryGetProperty("Message_ID", out var el)
                   && Guid.TryParse(el.GetString(), out var id)
                ? id
                : null;
        }
        catch { return null; }
    }

    /// <summary>Never persist gateway credentials into api_logs (they'd surface in the admin UI and backups).</summary>
    private static string MaskSecrets(string formBody) =>
        System.Text.RegularExpressions.Regex.Replace(
            formBody, "(apikey|secretkey)=[^&]*", "$1=***", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static async Task<string> ReadBodyAsync(HttpRequest req)
    {
        try
        {
            req.Body.Position = 0;
            using var r = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true);
            var body = await r.ReadToEndAsync();
            req.Body.Position = 0;
            return body;
        }
        catch { return "(unreadable)"; }
    }
}