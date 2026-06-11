namespace NetZeroGateway.Application.DTOs;
public class UpdateChannelConfigRequest
{
    public string ProviderName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = string.Empty;
}
