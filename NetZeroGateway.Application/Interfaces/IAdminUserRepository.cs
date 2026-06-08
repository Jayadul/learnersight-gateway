using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Interfaces;

public interface IAdminUserRepository : IRepository<AdminUser>
{
    Task<List<AdminUser>> FindByUsernameAsync(string username);
}
