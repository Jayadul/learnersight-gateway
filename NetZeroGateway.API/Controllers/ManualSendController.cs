using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Authorize]
[Route("api/manual-send")]
public class ManualSendController(IManualSendService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] ManualSendRequest request)
        => Accepted(await service.SendAsync(request));
}
