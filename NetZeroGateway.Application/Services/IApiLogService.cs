using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface IApiLogService
{
    Task<PagedResult<ApiLogDto>> GetPagedAsync(
        int page, int size,
        DateTimeOffset? from, DateTimeOffset? to,
        int? responseStatus, string? channel, string? callerIp = null, string? number = null,
        string? gatewayResult = null);
}
