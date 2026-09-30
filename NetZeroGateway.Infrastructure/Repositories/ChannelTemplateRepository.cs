using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class ChannelTemplateRepository(NetZeroGatewayDbContext db)
    : Repository<ChannelTemplate>(db), IChannelTemplateRepository
{
    public Task<List<ChannelTemplate>> GetAllByChannelAsync(Guid channelConfigId)
        => _set.Where(t => t.ChannelConfigId == channelConfigId)
               .OrderBy(t => t.Name)
               .ToListAsync();

    public Task<ChannelTemplate?> GetByChannelAndNameAsync(Guid channelConfigId, string name)
        => _set.FirstOrDefaultAsync(t =>
            t.ChannelConfigId == channelConfigId
            && t.Name.ToLower() == name.ToLower());
}
