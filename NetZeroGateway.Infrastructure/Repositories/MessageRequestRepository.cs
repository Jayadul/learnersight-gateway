using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class MessageRequestRepository(NetZeroGatewayDbContext db)
    : Repository<MessageRequest>(db), IMessageRequestRepository
{
    // The detail view names the channel each job was sent on, so the jobs' configs come
    // along. Without the ThenInclude the labels come back empty and every delivery reads
    // as if it went through an unnamed channel.
    public Task<MessageRequest?> GetWithJobsAsync(Guid id)
        => _set.Include(m => m.Jobs)
                   .ThenInclude(j => j.ChannelConfig)
               .FirstOrDefaultAsync(m => m.Id == id);

    public async Task<(List<MessageRequest> Items, int Total)> GetPagedAsync(
        int page, int size,
        DateTimeOffset? from, DateTimeOffset? to,
        string? toUser, MessageStatus? status, MessageSource? source,
        Guid? channelConfigId)
    {
        var q = _set.AsQueryable();
        if (from is not null) q = q.Where(m => m.ReceivedAt >= from);
        if (to is not null) q = q.Where(m => m.ReceivedAt <= to);
        if (!string.IsNullOrEmpty(toUser))
            q = q.Where(m => m.ToUser.Contains(toUser));
        if (status is not null) q = q.Where(m => m.OverallStatus == status);
        if (source is not null) q = q.Where(m => m.Source == source);
        // Filtering on the deliveries, not on the request: the request records channel
        // types, which no longer identify a gateway now that several may be active per
        // type. A fallback delivery counts — the message did go out through that gateway.
        if (channelConfigId is not null)
            q = q.Where(m => m.Jobs.Any(j => j.ChannelConfigId == channelConfigId));

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(m => m.ReceivedAt)
            .Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total);
    }
}
