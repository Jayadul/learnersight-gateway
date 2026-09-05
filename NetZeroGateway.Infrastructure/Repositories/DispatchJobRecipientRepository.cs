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

    public Task<List<DispatchJobRecipient>> GetPendingForChannelAsync(
        Guid channelConfigId, DateTimeOffset from, DateTimeOffset to, int limit)
        => _set.Include(r => r.DispatchJob)
               .Where(r => r.DeliveryStatus == DeliveryStatus.Pending
                        && r.DispatchJob!.ChannelConfigId == channelConfigId
                        && r.CreatedAt >= from
                        && r.CreatedAt <= to)
               .OrderBy(r => r.CreatedAt)
               .Take(limit)
               .ToListAsync();
}
