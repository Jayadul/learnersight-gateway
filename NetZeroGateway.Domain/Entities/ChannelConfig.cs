using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Domain.Entities;

public class ChannelConfig : BaseEntity
{
    public ChannelType ChannelType { get; set; }

    /// <summary>
    /// Which vendor serves this channel. Part of the sender-resolution key since Release
    /// 3 — a channel type no longer implies one provider. See ADR 0008.
    /// </summary>
    public Provider Provider { get; set; }

    /// <summary>
    /// Display text for the provider, e.g. "REVE Primary". Free-form and shown in the
    /// admin UI; <see cref="Provider"/> is what the dispatch path resolves on. Kept
    /// separate so an installation can label two REVE channels distinctly.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;
    /// <summary>Human-readable display name, e.g. "REVE Primary".</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>
    /// Tag-safe identifier used by labelled sends (<c>[sms:revemain::text]</c>).
    /// Lowercase alphanumeric only and unique per channel type. Hyphens are excluded on
    /// purpose: the combined form splits on <c>-wp:</c>, so a hyphen inside a slug
    /// would make the tag ambiguous to parse.
    /// </summary>
    public string LabelSlug { get; set; } = string.Empty;
    /// <summary>Several configs of one type may be active simultaneously.</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>
    /// Exactly one config per channel type is the default. Untagged sends resolve here.
    /// Gateway authentication no longer uses this config — since 2.1 it has its own
    /// credentials row, see ADR 0007.
    /// </summary>
    public bool IsDefault { get; set; }
    /// <summary>
    /// Seconds to wait before handing a job on this channel to the provider, applied to
    /// every attempt including fallback dispatches. Used to pace WhatsApp sends against
    /// provider rate limits. 0 disables it.
    /// <para>
    /// This does not govern the HTTP client's own retries — those are Polly's, configured
    /// by <c>HttpClients:Retry:DelaySeconds</c>.
    /// </para>
    /// </summary>
    public int DelaySeconds { get; set; }
    /// <summary>AES-256 encrypted JSON string.</summary>
    public string ConfigJson { get; set; } = string.Empty;
}
