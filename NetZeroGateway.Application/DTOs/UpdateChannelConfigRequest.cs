using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class UpdateChannelConfigRequest
{
    /// <summary>Which vendor serves this channel. Must have a registered sender.</summary>
    public Provider Provider { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>Optional; derived from Label when omitted. Lowercase alphanumeric only.</summary>
    public string? LabelSlug { get; set; }
    /// <summary>Seconds to wait before each send on this channel; 0 disables pacing.</summary>
    public int DelaySeconds { get; set; }
    public string ConfigJson { get; set; } = string.Empty;
}
