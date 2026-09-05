using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Interfaces;

public interface IDispatchJobRecipientRepository : IRepository<DispatchJobRecipient>
{
    /// <summary>Recipient rows for one job, in the order they were dispatched.</summary>
    Task<List<DispatchJobRecipient>> GetByDispatchJobIdAsync(Guid dispatchJobId);

    /// <summary>
    /// Adds a whole job's recipients in one round trip. Jobs routinely carry hundreds of
    /// numbers, so adding them one at a time would multiply the writes per send.
    /// </summary>
    Task AddRangeAsync(IEnumerable<DispatchJobRecipient> recipients);

    /// <summary>
    /// Rows on one channel still awaiting a delivery status, dispatched inside the
    /// window. Only Pending rows are returned: a row that already resolved, or that
    /// failed before the provider accepted it, has nothing left to sync. The job is
    /// eager-loaded because the sync needs its provider reference and dispatch time.
    /// </summary>
    Task<List<DispatchJobRecipient>> GetPendingForChannelAsync(
        Guid channelConfigId, DateTimeOffset from, DateTimeOffset to, int limit);
}
