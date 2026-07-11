using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Route("api/media")]
public class MediaController(IMediaLibraryService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Upload(
        IFormFile? file,
        [FromForm] string? customName,
        [FromForm] int expiryDays = 90,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            throw new InvalidOperationException("Please choose a file to upload.");

        await using var stream = file.OpenReadStream();
        var result = await service.UploadAsync(new UploadMediaRequest
        {
            Content = stream,
            OriginalFileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            CustomName = customName,
            ExpiryDays = expiryDays
        }, CurrentUsername, ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
        => Ok(await service.GetPagedAsync(page, pageSize, search));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id) => Ok(await service.GetByIdAsync(id));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await service.DeleteAsync(id, CurrentUsername);
        return NoContent();
    }

    private string CurrentUsername =>
        User.FindFirstValue(JwtRegisteredClaimNames.Name)
        ?? User.FindFirstValue(ClaimTypes.Name)
        ?? "unknown";
}
