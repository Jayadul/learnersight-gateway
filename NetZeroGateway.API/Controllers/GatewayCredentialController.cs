using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;

namespace NetZeroGateway.API.Controllers;

/// <summary>
/// The credentials third parties present to <c>/sendtext</c>. One row, so there is no
/// id in the route — SuperAdmin only, per authorization.json.
/// </summary>
[ApiController]
[Route("api/gateway-credentials")]
public class GatewayCredentialController(IGatewayCredentialService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await service.GetAsync());

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateGatewayCredentialRequest request)
        => Ok(await service.UpdateAsync(request));
}
