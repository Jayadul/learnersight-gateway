using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class CreateFallbackPolicyRequest
{
    public string Name { get; set; } = string.Empty;
    public ChannelType TriggerChannel { get; set; }
    public ChannelType FallbackChannel { get; set; }
    public bool IsGlobal { get; set; } = true;
    public string? Description { get; set; }
}
