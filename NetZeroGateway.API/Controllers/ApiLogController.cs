using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.Services;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Authorize]
[Route("api/api-logs")]
public class ApiLogController(IApiLogService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] int? responseStatus,
        [FromQuery] string? apiKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
        => Ok(await service.GetPagedAsync(page, pageSize, dateFrom, dateTo, responseStatus, apiKey));
}
