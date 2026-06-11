using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Services;

public class XSoftApiKeyService(
    IXSoftApiKeyRepository repo,
    ILogger<XSoftApiKeyService> logger
) : IXSoftApiKeyService
{
    public async Task<GenerateApiKeyResponse> GenerateAsync(string label)
    {
        var rawApiKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var rawSecretKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();

        var entity = new XSoftApiKey
        {
            Label = label,
            ApiKeyHash = BCrypt.Net.BCrypt.HashPassword(rawApiKey),
            SecretKeyHash = BCrypt.Net.BCrypt.HashPassword(rawSecretKey),
            IsActive = true
        };
        await repo.AddAsync(entity);
        await repo.SaveChangesAsync();

        logger.LogInformation("XSoft API key generated. Label={Label} Id={Id}", label, entity.Id);

        return new GenerateApiKeyResponse
        {
            Id = entity.Id,
            ApiKey = rawApiKey,
            SecretKey = rawSecretKey,
            Label = label
        };
    }

    public async Task<bool> ValidateAsync(string apiKey, string secretKey)
    {
        var activeKeys = await repo.FindAsync(k => k.IsActive);
        foreach (var key in activeKeys)
        {
            if (BCrypt.Net.BCrypt.Verify(apiKey, key.ApiKeyHash) &&
                BCrypt.Net.BCrypt.Verify(secretKey, key.SecretKeyHash))
            {
                key.LastUsedAt = DateTimeOffset.UtcNow;
                repo.Update(key);
                await repo.SaveChangesAsync();
                return true;
            }
        }
        return false;
    }

    public async Task<List<XSoftApiKeyDto>> GetAllAsync()
    {
        var keys = await repo.GetAllAsync();
        return keys.Select(k => new XSoftApiKeyDto
        {
            Id = k.Id,
            Label = k.Label,
            IsActive = k.IsActive,
            LastUsedAt = k.LastUsedAt,
            CreatedAt = k.CreatedAt
        }).ToList();
    }

    public async Task<bool> RevokeAsync(Guid id)
    {
        var key = await repo.GetByIdAsync(id);
        if (key is null) return false;
        key.IsActive = false;
        repo.Update(key);
        await repo.SaveChangesAsync();
        return true;
    }
}
