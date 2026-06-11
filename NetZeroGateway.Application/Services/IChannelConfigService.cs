using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public interface IChannelConfigService
{
    Task<List<ChannelConfigDto>> GetAllAsync(ChannelType? filter);
    Task<ChannelConfigDto> GetByIdAsync(Guid id);
    Task<ChannelConfigDto> CreateAsync(CreateChannelConfigRequest request);
    Task<ChannelConfigDto> UpdateAsync(Guid id, UpdateChannelConfigRequest request);
    Task DeleteAsync(Guid id);
    Task ActivateAsync(Guid id);
    Task<string> GetDecryptedConfigJsonAsync(Guid id);
}
