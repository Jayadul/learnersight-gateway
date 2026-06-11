using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.Services;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Authorize]
[Route("api/apikeys")]
[ApiExplorerSettings(GroupName = "admin")]
public class XSoftApiKeyController(IXSoftApiKeyService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await service.GetAllAsync());

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateLabelRequest request)
        => Ok(await service.GenerateAsync(request.Label));

    [HttpDelete("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var ok = await service.RevokeAsync(id);
        return ok ? NoContent() : NotFound();
    }
}

public record GenerateLabelRequest(string Label);
