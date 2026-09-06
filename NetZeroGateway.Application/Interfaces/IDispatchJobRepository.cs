using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

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
    /// <paramref name="deliveryStatus"/> filters on the job's recipient rows rather than
    /// the job itself: a Sent job can hold a mix of Delivered and Failed recipients, so
    /// the two filters are related but not interchangeable.
    /// </summary>
    Task<(List<DispatchJob> Items, int Total)> GetPagedAsync(
        int page, int size,
        DateTimeOffset? from, DateTimeOffset? to,
        ChannelType? channelType, JobStatus? status, string? toNumber,
        DeliveryStatus? deliveryStatus = null);
}
