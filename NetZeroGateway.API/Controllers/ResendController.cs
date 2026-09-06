using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Route("api/dispatch-jobs/recipients")]
public class ResendController(IResendService service) : ControllerBase
{
    /// <summary>
    /// Retries the named recipients, optionally through another active channel of the
    /// same type. Authorized at the ManualSend tier rather than SuperAdmin: resend is
    /// operational recovery, not configuration.
    /// </summary>
    [HttpPost("resend")]
    public async Task<ActionResult<ResendResultDto>> Resend([FromBody] ResendRequest request)
        => Ok(await service.ResendAsync(request));
}
