using System.Security.Claims;
using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Services;

public interface IJwtService
{
    string            IssueAccessToken(AdminUser user);
    string            IssueRefreshToken(AdminUser user);
    ClaimsPrincipal?  ValidateRefreshToken(string token);
}
