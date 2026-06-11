using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class CreateChannelConfigRequest
{
    public ChannelType ChannelType { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = string.Empty;
}