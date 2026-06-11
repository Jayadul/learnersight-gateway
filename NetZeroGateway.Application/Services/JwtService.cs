using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Services;

public class JwtService(IConfiguration config) : IJwtService
{
    private TokenValidationParameters ValidationParams(bool validateLifetime) => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),
        ValidateIssuer = true,
        ValidIssuer = config["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = config["Jwt:Audience"],
        ValidateLifetime = validateLifetime,
        ClockSkew = TimeSpan.Zero
    };

    public string IssueAccessToken(AdminUser user)
    {
        var mins = int.Parse(config["Jwt:AccessTokenExpiryMinutes"] ?? "15");
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Name,  user.Username),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role,               user.Role.ToString()),
            new Claim("type", "access")
        };
        return BuildToken(claims, TimeSpan.FromMinutes(mins));
    }

    public string IssueRefreshToken(AdminUser user)
    {
        var days = int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "7");
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("type", "refresh")
        };
        return BuildToken(claims, TimeSpan.FromDays(days));
    }

    public ClaimsPrincipal? ValidateRefreshToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, ValidationParams(true), out _);
            var typeClaim = principal.FindFirstValue("type");
            return typeClaim == "refresh" ? principal : null;
        }
        catch { return null; }
    }

    private string BuildToken(Claim[] claims, TimeSpan expiry)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.Add(expiry),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
