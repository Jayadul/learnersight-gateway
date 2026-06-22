using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Utilities;
using System.Text.Json;

namespace NetZeroGateway.API.Middleware;

public class GlobalExceptionMiddleware(
    RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception ex) { await HandleAsync(ctx, ex); }
    }

    private async Task HandleAsync(HttpContext ctx, Exception ex)
    {
        var config = ctx.RequestServices.GetRequiredService<IConfiguration>();
        var endpointPath = config[ConfigKeys.GatewayEndpointPath] ?? "/sendtext";
        var path = ctx.Request.Path.Value ?? "";

        ctx.Response.ContentType = "application/json";

        var isGateway = path.Equals(endpointPath, StringComparison.OrdinalIgnoreCase);

        (int status, string body) = ex switch
        {
            UnauthorizedException =>
                (401, isGateway
                    ? Reve("101", "REJECTD")
                    : Problem(401, "Unauthorized", ex.Message)),

            NotFoundException =>
                (404, isGateway
                    ? Reve("404", ex.Message)
                    : Problem(404, "Not Found", ex.Message)),

            InvalidOperationException =>
                (400, Problem(400, "Bad Request", ex.Message)),

            _ =>
                (500, isGateway
                    ? Reve("101", "REJECTD")
                    : Problem(500, "Internal Server Error",
                        "An unexpected error occurred.", ctx.TraceIdentifier))
        };

        if (status >= 500)
            logger.LogError(ex, "Unhandled exception on {Path}", path);
        else
            logger.LogWarning("{ExType} on {Path}: {Msg}", ex.GetType().Name, path, ex.Message);

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsync(body);
    }

    private static string Reve(string status, string text) =>
        JsonSerializer.Serialize(new { Status = status, Text = text });

    private static string Problem(int status, string title, string detail, string? traceId = null)
    {
        var pd = new ProblemDetails { Status = status, Title = title, Detail = detail };
        if (traceId is not null) pd.Extensions["traceId"] = traceId;
        return JsonSerializer.Serialize(pd);
    }
}