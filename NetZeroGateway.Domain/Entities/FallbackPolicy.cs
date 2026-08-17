using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Domain.Entities;

public class FallbackPolicy : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ChannelType TriggerChannel { get; set; }
    public ChannelType FallbackChannel { get; set; }

    /// <summary>
    /// Narrows the rule to failures on one specific channel. Null means "any channel of
    /// <see cref="TriggerChannel"/>", which is how every policy behaved before per-channel
    /// targeting existed.
    /// </summary>
    public Guid? TriggerChannelConfigId { get; set; }

    /// <summary>
    /// The exact channel to retry on. Null means "that type's default channel". Required
    /// when trigger and fallback are the same type, since otherwise the rule would point
    /// a channel at itself.
    /// </summary>
    public Guid? FallbackChannelConfigId { get; set; }

    /// <summary>
    /// Fallback stays within one channel type unless this is set. Crossing types changes
    /// both the delivery format and the cost of a message, so it is opt-in per policy
    /// rather than the default.
    /// </summary>
    public bool AllowCrossChannel { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsGlobal { get; set; } = true;
    public string? Description { get; set; }

    // Navigation
    public ChannelConfig? TriggerChannelConfig { get; set; }
    public ChannelConfig? FallbackChannelConfig { get; set; }
}
