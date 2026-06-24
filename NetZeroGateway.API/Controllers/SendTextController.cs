using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;
using System.Text.Json;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Route("sendtext")]
public class SendTextController(ISendTextOrchestratorService orchestrator) : ControllerBase
{
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
