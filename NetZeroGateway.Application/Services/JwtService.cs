using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Services;

public class JwtService(IConfiguration config) : IJwtService
{
    private string TypeClaim => config[ConfigKeys.JwtTokenTypeClaim] ?? "type";
    private string AccessValue => config[ConfigKeys.JwtAccessTypeValue] ?? "access";
    private string RefreshValue => config[ConfigKeys.JwtRefreshTypeValue] ?? "refresh";

    public string IssueAccessToken(AdminUser user)
    {
        var mins = int.Parse(config[ConfigKeys.JwtAccessExpiry] ?? "15");
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Name,  user.Username),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role,               user.Role.ToString()), // for server-side authorization
            new Claim("role",                        user.Role.ToString()), // short claim the SPA reads
            new Claim(TypeClaim,                     AccessValue)
        };
        return Build(claims, TimeSpan.FromMinutes(mins));
    }

    public string IssueRefreshToken(AdminUser user)
    {
        var days = int.Parse(config[ConfigKeys.JwtRefreshExpiry] ?? "7");
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(TypeClaim,                   RefreshValue)
        };
        return Build(claims, TimeSpan.FromDays(days));
    }

    public ClaimsPrincipal? ValidateRefreshToken(string token)
    {
        try
        {
            // MapInboundClaims must stay false: the default (true) rewrites "sub" to
            // ClaimTypes.NameIdentifier, so RefreshAsync's FindFirst(Sub) returns null
            // and every refresh fails with "Malformed refresh token."
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };
            var principal = handler.ValidateToken(token, ValidationParams(true), out _);
            return principal.FindFirstValue(TypeClaim) == RefreshValue ? principal : null;
        }
        catch { return null; }
    }

    private string Build(Claim[] claims, TimeSpan expiry)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config[ConfigKeys.JwtKey]!));
        var token = new JwtSecurityToken(
            issuer: config[ConfigKeys.JwtIssuer],
            audience: config[ConfigKeys.JwtAudience],
            claims: claims,
            expires: DateTime.UtcNow.Add(expiry),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private TokenValidationParameters ValidationParams(bool validateLifetime) => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(config[ConfigKeys.JwtKey]!)),
        ValidateIssuer = true,
        ValidIssuer = config[ConfigKeys.JwtIssuer],
        ValidateAudience = true,
        ValidAudience = config[ConfigKeys.JwtAudience],
        ValidateLifetime = validateLifetime,
        ClockSkew = TimeSpan.Zero
    };
}