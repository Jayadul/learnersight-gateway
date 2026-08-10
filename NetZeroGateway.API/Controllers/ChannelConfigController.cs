using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Route("api/channel-configs")]
public class ChannelConfigController(IChannelConfigService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ChannelType? channelType)
        => Ok(await service.GetAllAsync(channelType));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
        => Ok(await service.GetByIdAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateChannelConfigRequest request)
    {
        var result = await service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateChannelConfigRequest request)
        => Ok(await service.UpdateAsync(id, request));

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        await service.SetActiveAsync(id, true);
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await service.SetActiveAsync(id, false);
        return NoContent();
    }

    /// <summary>Promotes this config to be its type's default, demoting the previous one.</summary>
    [HttpPatch("{id:guid}/set-default")]
    public async Task<IActionResult> SetDefault(Guid id)
    {
        await service.SetDefaultAsync(id);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await service.DeleteAsync(id);
        return NoContent();
    }
}
