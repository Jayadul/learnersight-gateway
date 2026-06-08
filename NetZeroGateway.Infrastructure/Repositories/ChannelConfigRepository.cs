using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class ChannelConfigRepository(NetZeroGatewayDbContext db)
    : Repository<ChannelConfig>(db), IChannelConfigRepository
{
    public Task<ChannelConfig?> GetActiveByChannelTypeAsync(ChannelType type)
        => _set.FirstOrDefaultAsync(c => c.ChannelType == type && c.IsActive);

    public Task<List<ChannelConfig>> GetAllByChannelTypeAsync(ChannelType type)
        => _set.Where(c => c.ChannelType == type).ToListAsync();

    public async Task ActivateConfigAsync(Guid id, ChannelType type)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        await _set
            .Where(c => c.ChannelType == type && c.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false));
        await _set
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, true));
        await tx.CommitAsync();
    }
}
