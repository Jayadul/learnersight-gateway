using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Interfaces;

public interface IFallbackPolicyRepository : IRepository<FallbackPolicy>
{
    /// <summary>
    /// Every active policy, catch-all and channel-scoped alike — FallbackHandlerService
    /// does the scoping itself (TriggerChannelConfigId null vs specific, more specific
    /// wins). Hotfix batch 9, Task 9: this used to also require IsGlobal, a separate flag
    /// the admin UI defaults OFF for a new policy regardless of whether it is scoped —
    /// so a policy correctly scoped to one channel was silently invisible here unless an
    /// admin also flipped an oddly-worded "Apply this rule to all channels" toggle that
    /// made no sense to enable for a rule they had just scoped to one channel.
    /// </summary>
    Task<List<FallbackPolicy>> GetActivePoliciesAsync();
    Task<FallbackPolicy?> GetActiveByTriggerChannelAsync(ChannelType triggerChannel);

    /// <summary>Names of the policies pointing at this channel on either side. Empty if none.</summary>
    Task<List<string>> GetNamesReferencingChannelAsync(Guid channelConfigId);
}
