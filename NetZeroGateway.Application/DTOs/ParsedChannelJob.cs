using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class ParsedChannelJob
{
    public ChannelType ChannelType { get; set; }
    public string      Message     { get; set; } = string.Empty;
    public string?     MediaUrl    { get; set; }
    public string?     CampaignId  { get; set; }
}
