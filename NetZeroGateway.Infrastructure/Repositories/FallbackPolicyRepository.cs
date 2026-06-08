using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class FallbackPolicyRepository(NetZeroGatewayDbContext db)
    : Repository<FallbackPolicy>(db), IFallbackPolicyRepository
{
    public Task<List<FallbackPolicy>> GetActiveGlobalPoliciesAsync()
        => _set.Where(p => p.IsActive && p.IsGlobal).ToListAsync();

    public Task<FallbackPolicy?> GetActiveByTriggerChannelAsync(ChannelType triggerChannel)
        => _set.FirstOrDefaultAsync(p => p.IsActive && p.TriggerChannel == triggerChannel);
}
