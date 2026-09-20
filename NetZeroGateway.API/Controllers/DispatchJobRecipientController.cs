using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.API.Controllers;

/// <summary>
/// The merged, per-recipient Message Logs page (hotfix batch 3) and the resend action it
/// carries — previously split across MessageRequest's list view and the now-removed
/// DispatchLog page, neither of which was actually per-recipient.
/// </summary>
[ApiController]
[Route("api/dispatch-jobs/recipients")]
public class DispatchJobRecipientController(
    IDispatchJobRecipientService listService,
    IResendService resendService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] ChannelType? channelType,
        [FromQuery] Guid? channelConfigId,
        [FromQuery] DeliveryStatus? deliveryStatus,
        [FromQuery] string? recipient,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
        => Ok(await listService.GetPagedAsync(
            page, pageSize, dateFrom, dateTo, channelType, channelConfigId, deliveryStatus, recipient));

    /// <summary>
    /// Retries the named recipients, optionally through another active channel of the
    /// same type. Authorized at the ManualSend tier rather than SuperAdmin: resend is
    /// operational recovery, not configuration.
    /// </summary>
    [HttpPost("resend")]
    public async Task<ActionResult<ResendResultDto>> Resend([FromBody] ResendRequest request)
        => Ok(await resendService.ResendAsync(request));
}
