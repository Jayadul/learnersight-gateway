using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Application.Utilities;
using System.Text.Json;

namespace NetZeroGateway.API.Middleware;

/// <summary>
/// Guards POST /sendtext.
/// Two-layer validation:
///   1. apikey + secretkey form fields (BCrypt checked against XSoftApiKeys table)
///   2. X-XSoft-Token header (BCrypt checked against XSoftAppConfigs.SharedSecretHash)
/// If either check fails the request is rejected with REVE-format 401.
/// </summary>
public class ApiKeyAuthMiddleware(RequestDelegate next, ILogger<ApiKeyAuthMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var config = ctx.RequestServices.GetRequiredService<IConfiguration>();
        var endpointPath = config[ConfigKeys.GatewayEndpointPath] ?? "/sendtext";
        var tokenHeader = config[ConfigKeys.GatewayTokenHeader] ?? "X-XSoft-Token";

        if (!IsGateway(ctx, endpointPath)) { await next(ctx); return; }

        ctx.Request.EnableBuffering();
        ctx.Request.Body.Position = 0;

        var form = ctx.Request.HasFormContentType
            ? await ctx.Request.ReadFormAsync() : null;

        var apiKey = form?["apikey"].FirstOrDefault() ?? "";
        var secretKey = form?["secretkey"].FirstOrDefault() ?? "";
        ctx.Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            logger.LogWarning("[AUTH] Missing apikey/secretkey. IP={Ip}",
                ctx.Connection.RemoteIpAddress);
            await Reject(ctx); return;
        }

        var keySvc = ctx.RequestServices.GetRequiredService<IXSoftApiKeyService>();
        if (!await keySvc.ValidateAsync(apiKey, secretKey))
        {
            logger.LogWarning("[AUTH] Invalid apikey/secretkey. IP={Ip}",
                ctx.Connection.RemoteIpAddress);
            await Reject(ctx); return;
        }

        var xSoftRepo = ctx.RequestServices.GetRequiredService<IXSoftAppConfigRepository>();
        var xSoftCfg = await xSoftRepo.GetActiveAsync();

        if (xSoftCfg is not null)
        {
            var headerToken = ctx.Request.Headers[tokenHeader].FirstOrDefault() ?? "";
            if (string.IsNullOrWhiteSpace(headerToken) ||
                !BCrypt.Net.BCrypt.Verify(headerToken, xSoftCfg.SharedSecretHash))
            {
                logger.LogWarning("[AUTH] Invalid/missing {Header}. IP={Ip}",
                    tokenHeader, ctx.Connection.RemoteIpAddress);
                await Reject(ctx); return;
            }
        }

        await next(ctx);
    }

    private static bool IsGateway(HttpContext ctx, string path)
        => ctx.Request.Method == "POST" &&
           ctx.Request.Path.Equals(path, StringComparison.OrdinalIgnoreCase);

    private static Task Reject(HttpContext ctx)
    {
        ctx.Response.StatusCode = 401;
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsync(
            JsonSerializer.Serialize(new { Status = "101", Text = "REJECTD" }));
    }
}