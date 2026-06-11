using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class ManualSendRequest
{
    public ChannelType ChannelType { get; set; }
    public string ToNumbers { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? CampaignId { get; set; }
    public string? CallerID { get; set; }
}
