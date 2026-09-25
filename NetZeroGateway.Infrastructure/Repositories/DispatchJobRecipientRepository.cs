using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class DispatchJobRecipientRepository(NetZeroGatewayDbContext db)
    : Repository<DispatchJobRecipient>(db), IDispatchJobRecipientRepository
{
    public Task<List<DispatchJobRecipient>> GetByDispatchJobIdAsync(Guid dispatchJobId)
        => _set.Where(r => r.DispatchJobId == dispatchJobId)
               .OrderBy(r => r.CreatedAt)
               .ToListAsync();

    public Task AddRangeAsync(IEnumerable<DispatchJobRecipient> recipients)
        => _set.AddRangeAsync(recipients);

    public async Task<(List<DispatchJobRecipient> Items, int Total)> GetPagedAsync(
        int page, int size,
        DateTimeOffset? from, DateTimeOffset? to,
        ChannelType? channelType, Guid? channelConfigId,
        DeliveryStatus? deliveryStatus, string? recipient,
        string? failureReason = null)
    {
        var q = _set
            .Include(r => r.DispatchJob)
            .ThenInclude(j => j!.ChannelConfig)
            .AsQueryable();

        if (from is not null) q = q.Where(r => r.CreatedAt >= from);
        if (to is not null) q = q.Where(r => r.CreatedAt <= to);
        if (channelType is not null) q = q.Where(r => r.DispatchJob!.ChannelType == channelType);
        if (channelConfigId is not null) q = q.Where(r => r.DispatchJob!.ChannelConfigId == channelConfigId);
        if (deliveryStatus is not null) q = q.Where(r => r.DeliveryStatus == deliveryStatus);
        if (!string.IsNullOrEmpty(recipient)) q = q.Where(r => r.Recipient.Contains(recipient));
        if (!string.IsNullOrEmpty(failureReason)) q = q.Where(r => r.DispatchJob!.FailureReason == failureReason);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total);
    }

    public Task<List<DispatchJobRecipient>> GetPendingForChannelAsync(
        Guid channelConfigId, DateTimeOffset from, DateTimeOffset to, int limit)
        => _set.Include(r => r.DispatchJob)
               // Waiting rows, plus rows given up on as Unknown within the last 72 hours: a delivery
               // report can arrive late (phone off, carrier delay), and an Unknown row that was never
               // asked about again would miss it for good.
               .Where(r => (r.DeliveryStatus == DeliveryStatus.Pending
                            || (r.DeliveryStatus == DeliveryStatus.Unknown
                                && r.DispatchJob!.SentAt >= DateTimeOffset.UtcNow.AddHours(-72)))
                        && r.DispatchJob!.ChannelConfigId == channelConfigId
                        && r.CreatedAt >= from
                        && r.CreatedAt <= to)
               .OrderBy(r => r.CreatedAt)
               .Take(limit)
               .ToListAsync();
}
