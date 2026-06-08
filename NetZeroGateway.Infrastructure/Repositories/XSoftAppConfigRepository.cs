using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class XSoftAppConfigRepository(NetZeroGatewayDbContext db)
    : Repository<XSoftAppConfig>(db), IXSoftAppConfigRepository
{
    public Task<XSoftAppConfig?> GetActiveAsync()
        => _set.FirstOrDefaultAsync(c => c.IsActive);
}
