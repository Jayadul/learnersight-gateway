using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Interfaces;

public interface IApiLogRepository : IRepository<ApiLog>
{
    Task<(List<ApiLog> Items, int Total)> GetPagedAsync(
        int page, int size,
        DateTimeOffset? from, DateTimeOffset? to,
        int? responseStatus, string? channel, string? callerIp = null, string? number = null,
        string? gatewayResult = null);
}
