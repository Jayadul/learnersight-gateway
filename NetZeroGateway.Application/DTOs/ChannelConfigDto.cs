using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class ChannelConfigDto
{
    public Guid Id { get; set; }
    public ChannelType ChannelType { get; set; }
    /// <summary>Which vendor serves this channel; picks the sender at dispatch time.</summary>
    public Provider Provider { get; set; }
    /// <summary>Display text for the provider; <see cref="Provider"/> is what dispatch uses.</summary>
    public string ProviderName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>Tag-safe identifier for labelled sends, e.g. [sms:revemain::text].</summary>
    public string LabelSlug { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    /// <summary>Exactly one config per channel type is the default.</summary>
    public bool IsDefault { get; set; }
    /// <summary>Seconds to wait before each send on this channel; 0 disables pacing.</summary>
    public int DelaySeconds { get; set; }
    public Dictionary<string, string> ConfigFields { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
