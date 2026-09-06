using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Route("api/channel-balances")]
public class ChannelBalanceController(IChannelBalanceService service) : ControllerBase
{
    /// <summary>Cached balances. Does not call any provider — see ChannelBalanceService.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ChannelBalanceDto>>> GetAll()
        => Ok(await service.GetAllAsync());

    /// <summary>Reads one channel's balance from its provider now and caches the result.</summary>
    [HttpPost("{id:guid}/refresh")]
    public async Task<ActionResult<ChannelBalanceDto>> Refresh(Guid id)
        => Ok(await service.RefreshAsync(id));
}
