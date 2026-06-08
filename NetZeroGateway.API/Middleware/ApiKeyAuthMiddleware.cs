using System.Text.Json;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;

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
    private const string TokenHeader = "X-XSoft-Token";

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!IsSendText(ctx))
        {
            await next(ctx);
            return;
        }

        ctx.Request.EnableBuffering();
        ctx.Request.Body.Position = 0;

        var form = ctx.Request.HasFormContentType
            ? await ctx.Request.ReadFormAsync()
            : null;

        var apiKey    = form?["apikey"].FirstOrDefault()    ?? "";
        var secretKey = form?["secretkey"].FirstOrDefault() ?? "";
        ctx.Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            logger.LogWarning("[AUTH] Missing apikey/secretkey. IP={Ip}",
                ctx.Connection.RemoteIpAddress);
            await Reject(ctx);
            return;
        }

        // 1. Validate API key pair
        var keySvc = ctx.RequestServices.GetRequiredService<IXSoftApiKeyService>();
        if (!await keySvc.ValidateAsync(apiKey, secretKey))
        {
            logger.LogWarning("[AUTH] Invalid apikey/secretkey. IP={Ip}", ctx.Connection.RemoteIpAddress);
            await Reject(ctx);
            return;
        }

        // 2. Validate shared secret header (soft-fail when no config exists yet)
        var xSoftRepo = ctx.RequestServices.GetRequiredService<IXSoftAppConfigRepository>();
        var xSoftCfg  = await xSoftRepo.GetActiveAsync();

        if (xSoftCfg is not null)
        {
            var headerToken = ctx.Request.Headers[TokenHeader].FirstOrDefault() ?? "";
            if (string.IsNullOrWhiteSpace(headerToken) ||
                !BCrypt.Net.BCrypt.Verify(headerToken, xSoftCfg.SharedSecretHash))
            {
                logger.LogWarning(
                    "[AUTH] Invalid or missing X-XSoft-Token. IP={Ip}", ctx.Connection.RemoteIpAddress);
                await Reject(ctx);
                return;
            }
        }
        else
        {
            logger.LogDebug("[AUTH] No active XSoftAppConfig — skipping token header check");
        }

        await next(ctx);
    }

    private static bool IsSendText(HttpContext ctx)
        => ctx.Request.Method == "POST" &&
           ctx.Request.Path.Equals("/sendtext", StringComparison.OrdinalIgnoreCase);

    private static Task Reject(HttpContext ctx)
    {
        ctx.Response.StatusCode  = 401;
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsync(
            JsonSerializer.Serialize(new { Status = "101", Text = "REJECTD" }));
    }
}
