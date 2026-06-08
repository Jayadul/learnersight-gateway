using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class ApiLogRepository(NetZeroGatewayDbContext db)
    : Repository<ApiLog>(db), IApiLogRepository
{
    public async Task<(List<ApiLog> Items, int Total)> GetPagedAsync(
        int page, int size,
        DateTimeOffset? from, DateTimeOffset? to,
        int? responseStatus, string? apiKey)
    {
        var q = _set.AsQueryable();
        if (from           is not null) q = q.Where(l => l.RequestedAt    >= from);
        if (to             is not null) q = q.Where(l => l.RequestedAt    <= to);
        if (responseStatus is not null) q = q.Where(l => l.ResponseStatus == responseStatus);
        if (!string.IsNullOrEmpty(apiKey))
            q = q.Where(l => l.ApiKey != null && l.ApiKey.Contains(apiKey));

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(l => l.RequestedAt)
            .Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total);
    }
}
