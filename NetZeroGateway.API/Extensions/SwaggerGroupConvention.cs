using Microsoft.AspNetCore.Mvc.ApplicationModels;
using NetZeroGateway.Application.Utilities;

namespace NetZeroGateway.API.Extensions;

/// <summary>
/// Applies ApiExplorer GroupName to every controller based on its route prefix.
/// Reads group names from authorization.json so controllers need zero attributes.
///
/// Rule:
///   Route starts with the gateway endpoint path → "gateway" group
///   Everything else                             → "admin"  group
/// </summary>
public class SwaggerGroupConvention(IConfiguration config) : IControllerModelConvention
{
    private readonly string _gatewayPath =
        config[ConfigKeys.GatewayEndpointPath] ?? "/sendtext";
    private readonly string _gatewayGroup =
        config[ConfigKeys.GatewaySwaggerGroup] ?? "gateway";
    private readonly string _adminGroup =
        config[ConfigKeys.AdminSwaggerGroup] ?? "admin";

    public void Apply(ControllerModel controller)
    {
        // Determine group from the route template of the first action
        var route = controller.Selectors
            .Select(s => s.AttributeRouteModel?.Template ?? "")
            .FirstOrDefault() ?? "";

        // Normalise: strip leading slash for comparison
        var normalized = route.TrimStart('/');
        var gatewayNorm = _gatewayPath.TrimStart('/');

        var group = normalized.Equals(gatewayNorm, StringComparison.OrdinalIgnoreCase)
            ? _gatewayGroup
            : _adminGroup;

        controller.ApiExplorer.GroupName = group;
    }
}