using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Services;

public class FallbackPolicyService(IFallbackPolicyRepository repo) : IFallbackPolicyService
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
            IsGlobal = req.IsGlobal,
            Description = req.Description,
            IsActive = false
        };
        await repo.AddAsync(e);
        await repo.SaveChangesAsync();
        return ToDto(e);
    }

    public async Task<FallbackPolicyDto> UpdateAsync(Guid id, UpdateFallbackPolicyRequest req)
    {
        var e = await repo.GetByIdAsync(id)
            ?? throw new NotFoundException($"Policy {id} not found.");
        e.Name = req.Name; e.TriggerChannel = req.TriggerChannel;
        e.FallbackChannel = req.FallbackChannel; e.IsGlobal = req.IsGlobal;
        e.IsActive = req.IsActive; e.Description = req.Description;
        repo.Update(e);
        await repo.SaveChangesAsync();
        return ToDto(e);
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

    private static FallbackPolicyDto ToDto(FallbackPolicy e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        TriggerChannel = e.TriggerChannel,
        FallbackChannel = e.FallbackChannel,
        IsActive = e.IsActive,
        IsGlobal = e.IsGlobal,
        Description = e.Description,
        CreatedAt = e.CreatedAt
    };
}
