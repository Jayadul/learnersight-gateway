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

    public Task<List<string>> GetNamesReferencingChannelAsync(Guid channelConfigId)
        => _set.Where(p => p.TriggerChannelConfigId == channelConfigId
                        || p.FallbackChannelConfigId == channelConfigId)
               .Select(p => p.Name)
               // Names are not unique, and one channel can sit on both sides of a rule —
               // either would otherwise repeat a name in the message shown to the admin.
               .Distinct()
               .ToListAsync();

    // The read paths that feed the admin UI eager-load the named channels; without this
    // the DTO's channel labels come back null and the policy list cannot show which
    // specific channels a rule targets. The dispatch paths above deliberately do not
    // load them — they only need the ids.
    public override Task<List<FallbackPolicy>> GetAllAsync()
        => WithChannels().ToListAsync();

    public override Task<FallbackPolicy?> GetByIdAsync(Guid id)
        => WithChannels().FirstOrDefaultAsync(p => p.Id == id);

    private IQueryable<FallbackPolicy> WithChannels()
        => _set.Include(p => p.TriggerChannelConfig)
               .Include(p => p.FallbackChannelConfig);
}
