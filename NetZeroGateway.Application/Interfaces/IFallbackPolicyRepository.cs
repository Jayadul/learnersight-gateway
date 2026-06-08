using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Interfaces;

public interface IFallbackPolicyRepository : IRepository<FallbackPolicy>
{
    Task<List<FallbackPolicy>> GetActiveGlobalPoliciesAsync();
    Task<FallbackPolicy?>      GetActiveByTriggerChannelAsync(ChannelType triggerChannel);
}
