using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Services;

public class FallbackPolicyService(
    IFallbackPolicyRepository repo,
    IChannelConfigRepository configRepo
) : IFallbackPolicyService
{
    public async Task<List<FallbackPolicyDto>> GetAllAsync()
        => (await repo.GetAllAsync()).Select(ToDto).ToList();

    public async Task<FallbackPolicyDto> GetByIdAsync(Guid id)
        => ToDto(await repo.GetByIdAsync(id) ?? throw new NotFoundException($"Policy {id} not found."));

    public async Task<FallbackPolicyDto> CreateAsync(CreateFallbackPolicyRequest req)
    {
        var e = new FallbackPolicy
        {
            Name = req.Name,
            TriggerChannel = req.TriggerChannel,
            FallbackChannel = req.FallbackChannel,
            TriggerChannelConfigId = req.TriggerChannelConfigId,
            FallbackChannelConfigId = req.FallbackChannelConfigId,
            AllowCrossChannel = req.AllowCrossChannel,
            IsGlobal = req.IsGlobal,
            Description = req.Description,
            IsActive = false
        };
        await ValidateAsync(e);
        await repo.AddAsync(e);
        await repo.SaveChangesAsync();
        // Re-read so the channel navigations are loaded — ToDto reads its labels from
        // them, and on the entity we just built they are null.
        return ToDto(await repo.GetByIdAsync(e.Id) ?? e);
    }

    public async Task<FallbackPolicyDto> UpdateAsync(Guid id, UpdateFallbackPolicyRequest req)
    {
        var e = await repo.GetByIdAsync(id)
            ?? throw new NotFoundException($"Policy {id} not found.");
        e.Name = req.Name; e.TriggerChannel = req.TriggerChannel;
        e.FallbackChannel = req.FallbackChannel; e.IsGlobal = req.IsGlobal;
        e.IsActive = req.IsActive; e.Description = req.Description;
        e.TriggerChannelConfigId = req.TriggerChannelConfigId;
        e.FallbackChannelConfigId = req.FallbackChannelConfigId;
        e.AllowCrossChannel = req.AllowCrossChannel;
        await ValidateAsync(e);
        repo.Update(e);
        await repo.SaveChangesAsync();
        // Same reason as CreateAsync, plus: if the edit repointed a channel, the
        // navigations loaded before the change now describe the old ones.
        return ToDto(await repo.GetByIdAsync(e.Id) ?? e);
    }

    public async Task DeleteAsync(Guid id)
    {
        var e = await repo.GetByIdAsync(id) ?? throw new NotFoundException($"Policy {id} not found.");
        repo.Delete(e);
        await repo.SaveChangesAsync();
    }

    public async Task ToggleActiveAsync(Guid id)
    {
        var e = await repo.GetByIdAsync(id) ?? throw new NotFoundException($"Policy {id} not found.");
        e.IsActive = !e.IsActive;
        repo.Update(e);
        await repo.SaveChangesAsync();
    }

    /// <summary>
    /// Fallback stays within a channel type unless the policy opts in. Same-type rules
    /// must name both channels, because "WhatsApp falls back to WhatsApp" only means
    /// something once it says which WhatsApp channel.
    /// </summary>
    private async Task ValidateAsync(FallbackPolicy e)
    {
        if (e.TriggerChannel == e.FallbackChannel)
        {
            if (e.TriggerChannelConfigId is null || e.FallbackChannelConfigId is null)
                throw new InvalidOperationException(
                    "A fallback within the same channel type must name both the channel that "
                    + "fails and the channel to retry on.");

            if (e.TriggerChannelConfigId == e.FallbackChannelConfigId)
                throw new InvalidOperationException(
                    "A channel cannot fall back to itself. Choose a different channel of the same type.");
        }
        else if (!e.AllowCrossChannel)
        {
            throw new InvalidOperationException(
                "This rule falls back to a different channel type. Enable cross-channel "
                + "fallback on the policy to allow it.");
        }

        await EnsureChannelMatchesTypeAsync(e.TriggerChannelConfigId, e.TriggerChannel, "trigger");
        await EnsureChannelMatchesTypeAsync(e.FallbackChannelConfigId, e.FallbackChannel, "fallback");
    }

    /// <summary>Stops a policy pointing at a channel of the wrong type.</summary>
    private async Task EnsureChannelMatchesTypeAsync(Guid? configId, Domain.Enums.ChannelType type, string side)
    {
        if (configId is null) return;

        var config = await configRepo.GetByIdAsync(configId.Value)
            ?? throw new NotFoundException($"The selected {side} channel no longer exists.");

        if (config.ChannelType != type)
            throw new InvalidOperationException(
                $"The selected {side} channel is a {config.ChannelType} channel, but the rule's "
                + $"{side} type is {type}.");
    }

    private static FallbackPolicyDto ToDto(FallbackPolicy e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        TriggerChannel = e.TriggerChannel,
        FallbackChannel = e.FallbackChannel,
        TriggerChannelConfigId = e.TriggerChannelConfigId,
        FallbackChannelConfigId = e.FallbackChannelConfigId,
        AllowCrossChannel = e.AllowCrossChannel,
        TriggerChannelLabel = e.TriggerChannelConfig?.Label,
        FallbackChannelLabel = e.FallbackChannelConfig?.Label,
        IsActive = e.IsActive,
        IsGlobal = e.IsGlobal,
        Description = e.Description,
        CreatedAt = e.CreatedAt
    };
}
