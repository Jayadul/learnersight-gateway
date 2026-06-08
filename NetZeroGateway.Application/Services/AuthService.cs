using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Interfaces;

namespace NetZeroGateway.Application.Services;

public class AuthService(
    IAdminUserRepository repo,
    IJwtService          jwt,
    ILogger<AuthService> logger
) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var users = await repo.FindByUsernameAsync(request.Username);
        var user  = users.FirstOrDefault(u => u.IsActive)
            ?? throw new UnauthorizedException("Invalid username or password.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid username or password.");

        user.LastLoginAt = DateTimeOffset.UtcNow;
        repo.Update(user);
        await repo.SaveChangesAsync();

        logger.LogInformation("User {Username} logged in successfully", user.Username);

        return new LoginResponse
        {
            AccessToken  = jwt.IssueAccessToken(user),
            RefreshToken = jwt.IssueRefreshToken(user),
            ExpiresIn    = 900
        };
    }

    public async Task<LoginResponse> RefreshAsync(string refreshToken)
    {
        var principal = jwt.ValidateRefreshToken(refreshToken)
            ?? throw new UnauthorizedException("Invalid or expired refresh token.");

        var idStr = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(idStr, out var userId))
            throw new UnauthorizedException("Malformed refresh token.");

        var user = await repo.GetByIdAsync(userId)
            ?? throw new UnauthorizedException("User not found.");
        if (!user.IsActive)
            throw new UnauthorizedException("Account is inactive.");

        return new LoginResponse
        {
            AccessToken  = jwt.IssueAccessToken(user),
            RefreshToken = jwt.IssueRefreshToken(user),
            ExpiresIn    = 900
        };
    }
}
