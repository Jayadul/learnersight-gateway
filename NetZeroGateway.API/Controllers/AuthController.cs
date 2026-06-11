using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Route("api/auth")]
[ApiExplorerSettings(GroupName = "admin")]
public class AuthController(IAuthService service) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
        => Ok(await service.LoginAsync(request));

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        => Ok(await service.RefreshAsync(request.RefreshToken));

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // JWT is stateless; client discards tokens.
        // Implement a token blacklist here if needed.
        return Ok(new { message = "Logged out successfully." });
    }
}

public record RefreshRequest(string RefreshToken);
