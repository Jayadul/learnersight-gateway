using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Interfaces;

public interface IFallbackPolicyRepository : IRepository<FallbackPolicy>
{
    Task<List<FallbackPolicy>> GetActiveGlobalPoliciesAsync();
    Task<FallbackPolicy?> GetActiveByTriggerChannelAsync(ChannelType triggerChannel);

    /// <summary>Names of the policies pointing at this channel on either side. Empty if none.</summary>
    Task<List<string>> GetNamesReferencingChannelAsync(Guid channelConfigId);
}
