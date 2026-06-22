using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Authorize]
[Route("api/voice-reports")]
public class VoiceReportController(IVoiceReportService service) : ControllerBase
{
    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] VoiceReportSyncRequest request)
        => Ok(await service.SyncAsync(request));
}
