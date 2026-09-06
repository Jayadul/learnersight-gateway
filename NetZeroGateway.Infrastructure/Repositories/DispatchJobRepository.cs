using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class DispatchJobRepository(NetZeroGatewayDbContext db)
    : Repository<DispatchJob>(db), IDispatchJobRepository
{
    // The log views name the sending channel, so both read paths eager-load it. The
    // dispatch path deliberately does not — it already holds the config it is sending on.
    public Task<List<DispatchJob>> GetByMessageRequestIdAsync(Guid id)
        => _set.Include(j => j.ChannelConfig)
               .Where(j => j.MessageRequestId == id)
               .ToListAsync();

    public Task<List<DispatchJob>> GetFailedWithChannelAsync()
        => _set.Include(j => j.ChannelConfig)
               .Where(j => j.Status == JobStatus.Failed && j.FailureReason != null)
               .ToListAsync();

    public async Task<(List<DispatchJob> Items, int Total)> GetPagedAsync(
        int page, int size,
        DateTimeOffset? from, DateTimeOffset? to,
        ChannelType? channelType, JobStatus? status, string? toNumber,
        DeliveryStatus? deliveryStatus = null)
    {
        var q = _set.Include(j => j.ChannelConfig).AsQueryable();
        // Any-match: a job is included when at least one of its recipients is in that
        // state, since a batched job can hold several states at once.
        if (deliveryStatus is not null)
            q = q.Where(j => j.Recipients.Any(r => r.DeliveryStatus == deliveryStatus));
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
