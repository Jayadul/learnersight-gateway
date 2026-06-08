using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<LoginResponse> RefreshAsync(string refreshToken);
}
