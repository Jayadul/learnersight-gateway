using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.Services;

namespace NetZeroGateway.API.Controllers;

/// <summary>
/// Public, unauthenticated balance lookup for third-party integrators — a read-only
/// companion to <c>/sendtext</c>, at the API root for the same reason. Deliberately not
/// bound to REVE's <c>{Status,Text}</c> shape: this is a NetZero-only endpoint, so its
/// error shape is ours to choose.
/// <para>
/// No authentication: <c>callerID</c> alone resolves the channel (its value is the
/// channel's <c>labelSlug</c>, not a caller identity — named to match the field callers
/// already send to <c>/sendtext</c>). This trades access control for simplicity — anyone
/// who can guess or learn a label can read that channel's balance. Accepted deliberately;
/// flagged in <c>hotfix-batch-1.md</c> item 5 for the record.
/// </para>
/// </summary>
[ApiController]
[Route("balance")]
public class BalanceController(IChannelBalanceService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery(Name = "callerID")] string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return NotFound(new { status = "error", message = "Unknown or unsupported label" });

        var dto = await service.GetByLabelAsync(label);
        if (dto is null)
            return NotFound(new { status = "error", message = "Unknown or unsupported label" });

        return Ok(dto);
    }
}
