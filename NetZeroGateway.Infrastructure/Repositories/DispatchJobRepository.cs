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

    public Task<bool> ExistsForChannelAsync(Guid channelConfigId)
        => _set.AnyAsync(j => j.ChannelConfigId == channelConfigId);
}
