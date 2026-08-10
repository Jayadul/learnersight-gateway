using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Domain.Entities;

public class ChannelConfig : BaseEntity
{
    public ChannelType ChannelType { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    /// <summary>Human-readable display name, e.g. "REVE Primary".</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>
    /// Tag-safe identifier used by labelled sends (<c>[sms_label:revemain::text]</c>).
    /// Lowercase alphanumeric only and unique per channel type. Hyphens are excluded on
    /// purpose: the combined form splits on <c>-wp_label:</c>, so a hyphen inside a slug
    /// would make the tag ambiguous to parse.
    /// </summary>
    public string LabelSlug { get; set; } = string.Empty;
    /// <summary>Several configs of one type may be active simultaneously.</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>
    /// Exactly one config per channel type is the default. Untagged and unlabelled sends
    /// resolve here, and the gateway validates its apikey/secretkey against the default
    /// SMS config.
    /// </summary>
    public bool IsDefault { get; set; }
    /// <summary>AES-256 encrypted JSON string.</summary>
    public string ConfigJson { get; set; } = string.Empty;
}
