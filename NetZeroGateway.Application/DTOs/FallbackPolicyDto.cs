using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class FallbackPolicyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ChannelType TriggerChannel { get; set; }
    public ChannelType FallbackChannel { get; set; }
    /// <summary>Null = any channel of the trigger type.</summary>
    public Guid? TriggerChannelConfigId { get; set; }
    /// <summary>Null = that type's default channel.</summary>
    public Guid? FallbackChannelConfigId { get; set; }
    /// <summary>Required to let a policy fall back to a different channel type.</summary>
    public bool AllowCrossChannel { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Display labels so the UI need not re-resolve the ids.</summary>
    public string? TriggerChannelLabel { get; set; }
    public string? FallbackChannelLabel { get; set; }
    public bool IsGlobal { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
