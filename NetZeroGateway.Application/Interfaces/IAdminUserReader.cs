using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Interfaces;

/// <summary>
/// Read-only interface for admin user lookups used by Application layer services.
/// Implemented by AdminUserRepository in Infrastructure.
/// </summary>
public interface IAdminUserReader
{
    Task<AdminUser?> GetByIdAsync(Guid id);
    Task<List<AdminUser>> FindByUsernameAsync(string username);
    void Update(AdminUser user);
    Task<int> SaveChangesAsync();
}
