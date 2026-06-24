using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace NetZeroGateway.API.Extensions;

public class AuthorizationPolicyConvention(IConfiguration config) : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        var name = controller.ControllerName;
        var section = config.GetSection($"Authorization:Controllers:{name}");
        var defaults = config.GetSection("Authorization:Defaults");

        controller.ApiExplorer.GroupName =
            section["SwaggerGroup"] ?? defaults["SwaggerGroup"] ?? "admin";

        var requireAuth = section.Exists() && section["RequireAuth"] is not null
            ? bool.Parse(section["RequireAuth"]!)
            : bool.Parse(defaults["RequireAuth"] ?? "true");

        if (!requireAuth)
        {
            controller.Filters.Add(new AllowAnonymousFilter());
            return;
        }

        var roles = section.GetSection("Roles").Get<string[]>();
        if (roles is { Length: > 0 })
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole(roles)
                .Build();
            controller.Filters.Add(new AuthorizeFilter(policy));
        }
        else
        {
            controller.Filters.Add(new AuthorizeFilter());
        }
    }
}
