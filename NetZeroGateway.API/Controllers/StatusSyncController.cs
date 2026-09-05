using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Route("api/status-sync")]
public class StatusSyncController(IStatusSyncService service) : ControllerBase
{
    /// <summary>Channels whose provider offers a delivery status lookup.</summary>
    [HttpGet("channels")]
    public async Task<ActionResult<List<StatusSyncResultDto>>> GetChannels()
        => Ok(await service.GetSyncableChannelsAsync());

    /// <summary>
    /// Runs a sync for one channel now. Omitting the window syncs the previous full day
    /// in business time, the same window the nightly job uses.
    /// </summary>
    [HttpPost("channels/{id:guid}")]
    public async Task<ActionResult<StatusSyncResultDto>> Sync(
        Guid id, [FromBody] StatusSyncRequest request)
        => Ok(await service.SyncChannelAsync(id, request ?? new StatusSyncRequest()));
}
