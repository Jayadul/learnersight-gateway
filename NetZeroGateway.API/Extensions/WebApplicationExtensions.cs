using NetZeroGateway.API.Middleware;

namespace NetZeroGateway.API.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Registers the full middleware pipeline in the required order.
    /// Call after UseSwagger/UseSwaggerUI.
    /// </summary>
    public static WebApplication UseNetZeroGateway(this WebApplication app)
    {
        // 1. Global exception handler — wraps everything
        app.UseMiddleware<GlobalExceptionMiddleware>();

        // 2. API request/response logging — POST /sendtext only
        app.UseMiddleware<ApiLoggingMiddleware>();

        // 3. API key auth guard — POST /sendtext only, short-circuits if invalid
        app.UseMiddleware<ApiKeyAuthMiddleware>();

        // 4. Routing
        app.UseRouting();

        // 5. CORS
        app.UseCors("DefaultCors");

        // 6. JWT auth
        app.UseAuthentication();
        app.UseAuthorization();

        // 7. Controllers
        app.MapControllers();

        return app;
    }
}
