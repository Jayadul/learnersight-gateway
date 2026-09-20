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
}
