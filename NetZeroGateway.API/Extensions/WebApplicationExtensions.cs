using NetZeroGateway.API.Middleware;
using NetZeroGateway.Application.Utilities;

namespace NetZeroGateway.API.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseNetZeroGateway(this WebApplication app)
    {
        var corsPolicy = app.Configuration[ConfigKeys.AdminCorsPolicy] ?? "DefaultCors";

        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseMiddleware<ApiLoggingMiddleware>();
        app.UseRouting();
        app.UseCors(corsPolicy);
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        return app;
    }
}
