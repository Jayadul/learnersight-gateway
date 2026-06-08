using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class MessageRequestRepository(NetZeroGatewayDbContext db)
    : Repository<MessageRequest>(db), IMessageRequestRepository
{
    public Task<MessageRequest?> GetWithJobsAsync(Guid id)
        => _set.Include(m => m.Jobs).FirstOrDefaultAsync(m => m.Id == id);

    public async Task<(List<MessageRequest> Items, int Total)> GetPagedAsync(
        int page, int size,
        DateTimeOffset? from, DateTimeOffset? to,
        string? toUser, MessageStatus? status, MessageSource? source)
    {
        var q = _set.AsQueryable();
        if (from   is not null) q = q.Where(m => m.ReceivedAt    >= from);
        if (to     is not null) q = q.Where(m => m.ReceivedAt    <= to);
        if (!string.IsNullOrEmpty(toUser))
            q = q.Where(m => m.ToUser.Contains(toUser));
        if (status is not null) q = q.Where(m => m.OverallStatus == status);
        if (source is not null) q = q.Where(m => m.Source        == source);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(m => m.ReceivedAt)
            .Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total);
    }
}
