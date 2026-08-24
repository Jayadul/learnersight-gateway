using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class ChannelConfigRepository(NetZeroGatewayDbContext db)
    : Repository<ChannelConfig>(db), IChannelConfigRepository
{
    public Task<ChannelConfig?> GetDefaultByChannelTypeAsync(ChannelType type)
        => _set.FirstOrDefaultAsync(c => c.ChannelType == type && c.IsDefault);

    // Only active configs are addressable by slug: deactivating a channel must stop
    // labelled traffic reaching it, not silently keep routing to it.
    public Task<ChannelConfig?> GetActiveBySlugAsync(ChannelType type, string labelSlug)
        => _set.FirstOrDefaultAsync(c =>
            c.ChannelType == type && c.IsActive && c.LabelSlug == labelSlug);

    public Task<List<ChannelConfig>> GetAllByChannelTypeAsync(ChannelType type)
        => _set.Where(c => c.ChannelType == type).ToListAsync();

    public async Task SetDefaultAsync(Guid id, ChannelType type)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        // Clear first, then set: the unique partial index allows only one default per
        // type, so doing it in the other order would trip it mid-transaction.
        await _set
            .Where(c => c.ChannelType == type && c.IsDefault)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDefault, false));
        // A default must be usable, so promoting a config activates it too.
        await _set
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsDefault, true)
                .SetProperty(c => c.IsActive, true));
        await tx.CommitAsync();
    }
}
