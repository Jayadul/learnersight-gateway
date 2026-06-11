using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface IFallbackPolicyService
{
    Task<List<FallbackPolicyDto>> GetAllAsync();
    Task<FallbackPolicyDto> GetByIdAsync(Guid id);
    Task<FallbackPolicyDto> CreateAsync(CreateFallbackPolicyRequest request);
    Task<FallbackPolicyDto> UpdateAsync(Guid id, UpdateFallbackPolicyRequest request);
    Task DeleteAsync(Guid id);
    Task ToggleActiveAsync(Guid id);
}
