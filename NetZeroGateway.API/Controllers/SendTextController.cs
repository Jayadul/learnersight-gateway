using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;
using System.Text.Json;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Route("sendtext")]
[ApiExplorerSettings(GroupName = "gateway")]
public class SendTextController(ISendTextOrchestratorService orchestrator) : ControllerBase
{
    /// <summary>
    /// Mirror of the REVE SMS HTTP API. x-soft calls this endpoint with no knowledge
    /// of the gateway. Returns REVE-format JSON regardless of errors.
    /// </summary>
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<ContentResult> SendText([FromForm] SendTextRequest request)
    {
        try
        {
            var result = await orchestrator.ProcessAsync(request);
            return Content(JsonSerializer.Serialize(result), "application/json");
        }
        catch
        {
            return Content(
                JsonSerializer.Serialize(SendTextResponse.Rejected()),
                "application/json");
        }
    }
}
