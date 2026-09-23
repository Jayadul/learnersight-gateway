using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Route("api/manual-send")]
public class ManualSendController(
    IManualSendService service, IChannelConfigService channelConfigService
) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] ManualSendRequest request)
        => Accepted(await service.SendAsync(request));

    /// <summary>
    /// Sends a Meta pre-approved WhatsApp template — a structured payload (template name
    /// + ordered variables), never reachable through /sendtext's tag grammar. Same auth
    /// tier as the rest of this controller, not SuperAdmin.
    /// </summary>
    [HttpPost("template")]
    public async Task<IActionResult> SendTemplate([FromBody] SendTemplateRequest request)
        => Ok(await service.SendTemplateAsync(request));

    /// <summary>
    /// Which gateways exist, for a "Send Through" picker or a gateway filter — id, label,
    /// labelSlug, default flag only, no credentials. Deliberately not on
    /// ChannelConfigController: that whole controller is SuperAdmin-only (it also holds
    /// the mutation endpoints), but this controller already isn't, and knowing which
    /// active gateways exist is something any page an Admin-role user can reach may
    /// legitimately need.
    /// </summary>
    [HttpGet("channels")]
    public async Task<IActionResult> GetChannelOptions([FromQuery] ChannelType? channelType)
        => Ok(await channelConfigService.GetOptionsAsync(channelType));
}
