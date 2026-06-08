using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class AdminUserRepository(NetZeroGatewayDbContext db)
    : Repository<AdminUser>(db), IAdminUserRepository
{
    public Task<List<AdminUser>> FindByUsernameAsync(string username)
        => _set.Where(u => u.Username == username).ToListAsync();
}
