using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface IAdminUserService
{
    Task<List<AdminUserDto>> GetAllAsync();
    Task<AdminUserDto> GetByIdAsync(Guid id);
    Task<AdminUserDto> CreateAsync(CreateAdminUserRequest request);
    Task<AdminUserDto> UpdateAsync(Guid id, UpdateAdminUserRequest request);
    Task DeleteAsync(Guid id);
}
