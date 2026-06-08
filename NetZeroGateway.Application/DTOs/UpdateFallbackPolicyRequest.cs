using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class UpdateFallbackPolicyRequest
{
    public string      Name            { get; set; } = string.Empty;
    public ChannelType TriggerChannel  { get; set; }
    public ChannelType FallbackChannel { get; set; }
    public bool        IsGlobal        { get; set; }
    public bool        IsActive        { get; set; }
    public string?     Description     { get; set; }
}
