using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.DTOs;

/// <summary>
/// The minimal, non-sensitive slice of a channel config a non-SuperAdmin page needs to
/// let an operator pick a gateway — a manual-send "Send Through" dropdown, a log page's
/// gateway filter. No credentials, no balance, nothing <see cref="ChannelConfigDto"/>
/// carries beyond identity: full <c>ChannelConfigController</c> access (including
/// mutations) stays SuperAdmin-only, but knowing which active gateways exist does not.
/// </summary>
public class ChannelOptionDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string LabelSlug { get; set; } = string.Empty;
    public ChannelType ChannelType { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    /// <summary>
    /// Whether this channel has a Template API Token configured (hotfix batch 3) — lets
    /// the Manual Send page offer "Send Template" mode only for channels that can
    /// actually serve it, without exposing the token itself through this endpoint.
    /// </summary>
    public bool HasTemplateToken { get; set; }
}
