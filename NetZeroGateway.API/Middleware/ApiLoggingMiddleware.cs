using NetZeroGateway.Application.Interfaces;
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
        if (ctx.Request.Method != "POST" ||
            !ctx.Request.Path.Equals("/sendtext", StringComparison.OrdinalIgnoreCase))
        {
            await next(ctx);
            return;
        }

        // Buffer request so it can be read more than once
        ctx.Request.EnableBuffering();

        var reqBody = await ReadBodyAsync(ctx.Request);
        ctx.Request.Body.Position = 0;

        // Swap response stream to capture output
        var originalBody     = ctx.Response.Body;
        using var captureMs  = new MemoryStream();
        ctx.Response.Body    = captureMs;

        var sw = Stopwatch.StartNew();
        await next(ctx);
        sw.Stop();

        // Read captured response
        captureMs.Position = 0;
        var respBody = await new StreamReader(captureMs).ReadToEndAsync();

        // Copy back to real stream
        captureMs.Position = 0;
        await captureMs.CopyToAsync(originalBody);
        ctx.Response.Body = originalBody;

        // Extract apikey / toUser from form (best-effort)
        string? apiKeyVal = null;
        string? toUserVal = null;
        try
        {
            if (ctx.Request.HasFormContentType)
            {
                ctx.Request.Body.Position = 0;
                var form  = await ctx.Request.ReadFormAsync();
                ctx.Request.Body.Position = 0;
                apiKeyVal = form["apikey"].FirstOrDefault();
                toUserVal = form["toUser"].FirstOrDefault();
            }
        }
        catch { /* swallow — logging must never break the pipeline */ }

        // Persist log record
        try
        {
            await using var scope = ctx.RequestServices.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IApiLogRepository>();

            await repo.AddAsync(new ApiLog
            {
                ApiKey         = apiKeyVal,
                ToUser         = toUserVal,
                RawBody        = reqBody,
                ResponseBody   = respBody,
                CallerIp       = ctx.Connection.RemoteIpAddress?.ToString() ?? "",
                ResponseStatus = ctx.Response.StatusCode,
                DurationMs     = sw.ElapsedMilliseconds,
                RequestedAt    = DateTimeOffset.UtcNow
            });
            await repo.SaveChangesAsync();

            logger.LogInformation(
                "[API_LOG] {Status} in {Ms}ms | To={ToUser} | IP={Ip}",
                ctx.Response.StatusCode, sw.ElapsedMilliseconds, toUserVal,
                ctx.Connection.RemoteIpAddress);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[API_LOG] Failed to persist API log record");
        }
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        try
        {
            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            return body;
        }
        catch { return "(unreadable)"; }
    }
}
