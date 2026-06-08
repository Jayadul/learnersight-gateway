using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Interfaces;

public interface IChannelConfigRepository : IRepository<ChannelConfig>
{
    Task<ChannelConfig?>       GetActiveByChannelTypeAsync(ChannelType type);
    Task<List<ChannelConfig>>  GetAllByChannelTypeAsync(ChannelType type);
    Task                       ActivateConfigAsync(Guid id, ChannelType type);
}
