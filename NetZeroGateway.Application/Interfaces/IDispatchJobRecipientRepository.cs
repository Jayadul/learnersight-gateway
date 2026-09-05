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
}
