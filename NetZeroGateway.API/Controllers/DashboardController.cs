using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.Services;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
[ApiExplorerSettings(GroupName = "admin")]
public class DashboardController(IDashboardService service) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
        => Ok(await service.GetSummaryAsync());

    [HttpGet("timeline")]
    public async Task<IActionResult> Timeline([FromQuery] int days = 7)
        => Ok(await service.GetTimelineAsync(days));

    [HttpGet("channel-breakdown")]
    public async Task<IActionResult> ChannelBreakdown()
        => Ok(await service.GetChannelBreakdownAsync());

    [HttpGet("failure-reasons")]
    public async Task<IActionResult> FailureReasons([FromQuery] int top = 5)
        => Ok(await service.GetTopFailureReasonsAsync(top));
}
