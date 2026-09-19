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
    public Task<ContentResult> SendText([FromForm] SendTextRequest request) => Dispatch(request);

    // GET variant of the same endpoint, for integrators whose platform cannot easily do
    // POST. Same fields as query-string params, same orchestrator call, same response
    // shape — no duplicated logic. POST stays the documented default: it keeps
    // credentials off the URL, which GET cannot.
    [HttpGet]
    public Task<ContentResult> SendTextGet([FromQuery] SendTextQueryRequest request)
        => Dispatch(new SendTextRequest
        {
            ApiKey = request.ApiKey,
            SecretKey = request.SecretKey,
            CallerID = request.CallerID,
            ToUser = request.ToUser,
            MessageContent = request.MessageContent,
        });

    private async Task<ContentResult> Dispatch(SendTextRequest request)
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
