using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class GatewayCredentialRepository(NetZeroGatewayDbContext db)
    : Repository<GatewayCredential>(db), IGatewayCredentialRepository
{
    public Task<GatewayCredential?> GetCurrentAsync()
        => _set.OrderBy(c => c.CreatedAt).FirstOrDefaultAsync();
}
