using System.Text.Json;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public class ChannelConfigService(
    IChannelConfigRepository repo,
    IEncryptionService       enc
) : IChannelConfigService
{
    public async Task<List<ChannelConfigDto>> GetAllAsync(ChannelType? filter)
    {
        var list = filter is null
            ? await repo.GetAllAsync()
            : await repo.GetAllByChannelTypeAsync(filter.Value);
        return list.Select(ToDto).ToList();
    }

    public async Task<ChannelConfigDto> GetByIdAsync(Guid id)
        => ToDto(await repo.GetByIdAsync(id)
            ?? throw new NotFoundException($"ChannelConfig {id} not found."));

    public async Task<ChannelConfigDto> CreateAsync(CreateChannelConfigRequest req)
    {
        var entity = new ChannelConfig
        {
            ChannelType  = req.ChannelType,
            ProviderName = req.ProviderName,
            Label        = req.Label,
            ConfigJson   = enc.Encrypt(req.ConfigJson),
            IsActive     = false
        };
        await repo.AddAsync(entity);
        await repo.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<ChannelConfigDto> UpdateAsync(Guid id, UpdateChannelConfigRequest req)
    {
        var entity = await repo.GetByIdAsync(id)
            ?? throw new NotFoundException($"ChannelConfig {id} not found.");
        entity.ProviderName = req.ProviderName;
        entity.Label        = req.Label;
        entity.ConfigJson   = enc.Encrypt(req.ConfigJson);
        repo.Update(entity);
        await repo.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await repo.GetByIdAsync(id)
            ?? throw new NotFoundException($"ChannelConfig {id} not found.");
        if (entity.IsActive)
            throw new InvalidOperationException(
                "Cannot delete an active channel config. Deactivate it first.");
        repo.Delete(entity);
        await repo.SaveChangesAsync();
    }

    public async Task ActivateAsync(Guid id)
    {
        var entity = await repo.GetByIdAsync(id)
            ?? throw new NotFoundException($"ChannelConfig {id} not found.");
        await repo.ActivateConfigAsync(id, entity.ChannelType);
    }

    public async Task<string> GetDecryptedConfigJsonAsync(Guid id)
    {
        var entity = await repo.GetByIdAsync(id)
            ?? throw new NotFoundException($"ChannelConfig {id} not found.");
        return enc.Decrypt(entity.ConfigJson);
    }

    private ChannelConfigDto ToDto(ChannelConfig c)
    {
        var masked = new Dictionary<string, string>();
        try
        {
            var plain = enc.Decrypt(c.ConfigJson);
            var dict  = JsonSerializer.Deserialize<Dictionary<string, string>>(plain);
            if (dict is not null)
                foreach (var kv in dict)
                    masked[kv.Key] = IsSecret(kv.Key) ? "***" : kv.Value;
        }
        catch { masked["error"] = "unable_to_decode"; }

        return new ChannelConfigDto
        {
            Id           = c.Id,
            ChannelType  = c.ChannelType,
            ProviderName = c.ProviderName,
            Label        = c.Label,
            IsActive     = c.IsActive,
            ConfigFields = masked,
            CreatedAt    = c.CreatedAt,
            UpdatedAt    = c.UpdatedAt
        };
    }

    private static bool IsSecret(string key) =>
        key is "apikey" or "secretkey" or "access_token" or "password" or "authUserId";
}
