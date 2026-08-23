using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class ParsedChannelJob
{
    public ChannelType ChannelType { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? CampaignId { get; set; }
    /// <summary>
    /// Slug from a labelled tag (<c>[sms_label:revemain::…]</c>) naming which channel of
    /// this type to use. Null means "the type's default channel".
    /// </summary>
    public string? ChannelLabelSlug { get; set; }
    /// <summary>
    /// Per-message voice lot from <c>[call::campaignId||lotNumber]</c>. Overlaid onto the
    /// channel config at dispatch time rather than stored on the DispatchJob, so it is
    /// deliberately not persisted; null means "use the channel's configured lot".
    /// </summary>
    public string? LotNumber { get; set; }
}
