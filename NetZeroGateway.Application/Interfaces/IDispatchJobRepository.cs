using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Interfaces;

public interface IDispatchJobRepository : IRepository<DispatchJob>
{
    Task<List<DispatchJob>> GetByMessageRequestIdAsync(Guid id);

    /// <summary>
    /// Failed jobs with their channel loaded, for the dashboard's failure breakdown.
    /// The generic Find cannot eager-load, so the labels would come back null.
    /// </summary>
    Task<List<DispatchJob>> GetFailedWithChannelAsync();

    /// <summary>
    /// Whether this channel has ever sent anything (hotfix batch 9, Task 16). The FK is
    /// Restrict, same as FallbackPolicy's, so deleting a channel with history would
    /// otherwise surface as a raw, unhandled DB constraint violation instead of a
    /// readable error — checked here so ChannelConfigService can refuse the delete the
    /// same clean way it already does for the default flag and fallback-policy references.
    /// </summary>
    Task<bool> ExistsForChannelAsync(Guid channelConfigId);
}
