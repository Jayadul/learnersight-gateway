using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;

namespace NetZeroGateway.API.Controllers;

/// <summary>
/// Manages the templates registered on a WhatsApp Template channel (hotfix batch 9, Task
/// 1). Nested under ChannelConfig's own route and SuperAdmin-gated the same way — reading
/// the list for a send-time dropdown goes through ManualSendController's channel options
/// instead, which is already open to every admin role.
/// </summary>
[ApiController]
[Route("api/channel-configs/{channelConfigId:guid}/templates")]
public class ChannelTemplateController(IChannelTemplateService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(Guid channelConfigId)
        => Ok(await service.GetAllAsync(channelConfigId));

    [HttpPost]
    public async Task<IActionResult> Create(Guid channelConfigId, [FromBody] CreateChannelTemplateRequest request)
        => Ok(await service.CreateAsync(channelConfigId, request));

    [HttpPut("{templateId:guid}")]
    public async Task<IActionResult> Update(Guid channelConfigId, Guid templateId, [FromBody] UpdateChannelTemplateRequest request)
        => Ok(await service.UpdateAsync(channelConfigId, templateId, request));

    [HttpDelete("{templateId:guid}")]
    public async Task<IActionResult> Delete(Guid channelConfigId, Guid templateId)
    {
        await service.DeleteAsync(channelConfigId, templateId);
        return NoContent();
    }
}
