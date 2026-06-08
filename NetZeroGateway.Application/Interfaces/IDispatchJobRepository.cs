using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Interfaces;

public interface IDispatchJobRepository : IRepository<DispatchJob>
{
    Task<List<DispatchJob>> GetByMessageRequestIdAsync(Guid id);
    Task<(List<DispatchJob> Items, int Total)> GetPagedAsync(
        int page, int size,
        DateTimeOffset? from, DateTimeOffset? to,
        ChannelType? channelType, JobStatus? status, string? toNumber);
}
