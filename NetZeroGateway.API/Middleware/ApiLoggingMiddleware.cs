using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;
using System.Diagnostics;
using System.Text;

namespace NetZeroGateway.API.Middleware;

/// <summary>
/// Logs all POST /sendtext requests and responses to the ApiLogs table.
/// Captures request body (form) and response body without breaking the pipeline.
/// </summary>
public class ApiLoggingMiddleware(RequestDelegate next, ILogger<ApiLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var config = ctx.RequestServices.GetRequiredService<IConfiguration>();
        var endpointPath = config[ConfigKeys.GatewayEndpointPath] ?? "/sendtext";

        if (ctx.Request.Method != "POST" ||
            !ctx.Request.Path.Equals(endpointPath, StringComparison.OrdinalIgnoreCase))
        {
            await next(ctx); return;
        }

        ctx.Request.EnableBuffering();
        var reqBody = await ReadBodyAsync(ctx.Request);
        ctx.Request.Body.Position = 0;

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

        string? apiKeyVal = null, toUserVal = null;
        try
        {
            if (ctx.Request.HasFormContentType)
            {
                ctx.Request.Body.Position = 0;
                var form = await ctx.Request.ReadFormAsync();
                ctx.Request.Body.Position = 0;
                apiKeyVal = form["apikey"].FirstOrDefault();
                toUserVal = form["toUser"].FirstOrDefault();
            }
        }
        catch { /* never break pipeline */ }

        try
        {
            await using var scope = ctx.RequestServices.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IApiLogRepository>();
            await repo.AddAsync(new ApiLog
            {
                ApiKey = apiKeyVal,
                ToUser = toUserVal,
                RawBody = reqBody,
                ResponseBody = respBody,
                CallerIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "",
                ResponseStatus = ctx.Response.StatusCode,
                DurationMs = sw.ElapsedMilliseconds,
                RequestedAt = DateTimeOffset.UtcNow
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