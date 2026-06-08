using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.Exceptions;

namespace NetZeroGateway.API.Middleware;

public class GlobalExceptionMiddleware(
    RequestDelegate                     next,
    ILogger<GlobalExceptionMiddleware>  logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception ex) { await HandleAsync(ctx, ex); }
    }

    private async Task HandleAsync(HttpContext ctx, Exception ex)
    {
        var path = ctx.Request.Path.Value ?? "";
        ctx.Response.ContentType = "application/json";

        switch (ex)
        {
            case UnauthorizedException:
                logger.LogWarning("Unauthorized: {Path} — {Message}", path, ex.Message);
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsync(IsGatewayPath(path)
                    ? JsonSerializer.Serialize(new { Status = "101", Text = "REJECTD" })
                    : JsonSerializer.Serialize(ProblemOf(401, "Unauthorized", ex.Message)));
                return;

            case NotFoundException:
                logger.LogWarning("NotFound: {Path} — {Message}", path, ex.Message);
                ctx.Response.StatusCode = 404;
                await ctx.Response.WriteAsync(IsGatewayPath(path)
                    ? JsonSerializer.Serialize(new { Status = "404", Text = ex.Message })
                    : JsonSerializer.Serialize(ProblemOf(404, "Not Found", ex.Message)));
                return;

            case InvalidOperationException:
                logger.LogWarning("BadRequest: {Path} — {Message}", path, ex.Message);
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(
                    ProblemOf(400, "Bad Request", ex.Message)));
                return;

            default:
                logger.LogError(ex, "Unhandled exception on {Path}", path);
                ctx.Response.StatusCode = 500;
                await ctx.Response.WriteAsync(IsGatewayPath(path)
                    ? JsonSerializer.Serialize(new { Status = "101", Text = "REJECTD" })
                    : JsonSerializer.Serialize(ProblemOf(500, "Internal Server Error",
                        "An unexpected error occurred.", ctx.TraceIdentifier)));
                return;
        }
    }

    private static bool IsGatewayPath(string path)
        => path.Equals("/sendtext", StringComparison.OrdinalIgnoreCase);

    private static object ProblemOf(
        int status, string title, string detail, string? traceId = null)
    {
        var pd = new ProblemDetails
        {
            Status = status,
            Title  = title,
            Detail = detail
        };
        if (traceId is not null) pd.Extensions["traceId"] = traceId;
        return pd;
    }
}
