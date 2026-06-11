using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;

namespace NetZeroGateway.Application.Services;

public class ApiLogService(IApiLogRepository repo) : IApiLogService
{
    public async Task<PagedResult<ApiLogDto>> GetPagedAsync(
        int page, int size,
        DateTimeOffset? from, DateTimeOffset? to,
        int? responseStatus, string? apiKey)
    {
        var (items, total) = await repo.GetPagedAsync(
            page, size, from, to, responseStatus, apiKey);

        return new PagedResult<ApiLogDto>
        {
            Items = items.Select(l => new ApiLogDto
            {
                Id = l.Id,
                ApiKey = l.ApiKey,
                ToUser = l.ToUser,
                RawBody = l.RawBody,
                ResponseBody = l.ResponseBody,
                CallerIp = l.CallerIp,
                ResponseStatus = l.ResponseStatus,
                DurationMs = l.DurationMs,
                RequestedAt = l.RequestedAt,
                MessageRequestId = l.MessageRequestId
            }).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = size
        };
    }
}
