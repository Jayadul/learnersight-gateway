using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Interfaces;

public interface IChannelTemplateService
{
    Task<List<ChannelTemplateDto>> GetAllAsync(Guid channelConfigId);
    Task<ChannelTemplateDto> CreateAsync(Guid channelConfigId, CreateChannelTemplateRequest req);
    Task<ChannelTemplateDto> UpdateAsync(Guid channelConfigId, Guid templateId, UpdateChannelTemplateRequest req);
    Task DeleteAsync(Guid channelConfigId, Guid templateId);
}
