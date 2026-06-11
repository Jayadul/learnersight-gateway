using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class DispatchJobRepository(NetZeroGatewayDbContext db)
    : Repository<DispatchJob>(db), IDispatchJobRepository
{
    public Task<List<DispatchJob>> GetByMessageRequestIdAsync(Guid id)
        => _set.Where(j => j.MessageRequestId == id).ToListAsync();

    public async Task<(List<DispatchJob> Items, int Total)> GetPagedAsync(
        int page, int size,
        DateTimeOffset? from, DateTimeOffset? to,
        ChannelType? channelType, JobStatus? status, string? toNumber)
    {
        var q = _set.AsQueryable();
        if (from is not null) q = q.Where(j => j.CreatedAt >= from);
        if (to is not null) q = q.Where(j => j.CreatedAt <= to);
        if (channelType is not null) q = q.Where(j => j.ChannelType == channelType);
        if (status is not null) q = q.Where(j => j.Status == status);
        if (!string.IsNullOrEmpty(toNumber))
            q = q.Where(j => j.ToNumber.Contains(toNumber));

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total);
    }
}
