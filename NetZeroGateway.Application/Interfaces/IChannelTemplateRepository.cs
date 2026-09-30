using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Interfaces;

public interface IChannelTemplateRepository : IRepository<ChannelTemplate>
{
    Task<List<ChannelTemplate>> GetAllByChannelAsync(Guid channelConfigId);
    Task<ChannelTemplate?> GetByChannelAndNameAsync(Guid channelConfigId, string name);
}
