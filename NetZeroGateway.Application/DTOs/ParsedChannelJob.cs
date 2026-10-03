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
    /// Sender id for this message only, overlaid onto the channel config at dispatch time.
    /// Null means "use the channel's configured callerID". Only the admin panel sets it —
    /// gateway callers are authenticated by their own callerID and cannot override it.
    /// </summary>
    public string? CallerId { get; set; }
    /// <summary>
    /// Per-message voice lot from <c>[call::campaignId||lotNumber]</c>. Overlaid onto the
    /// channel config at dispatch time rather than stored on the DispatchJob, so it is
    /// deliberately not persisted; null means "use the channel's configured lot".
    /// </summary>
    public string? LotNumber { get; set; }
    /// <summary>
    /// Set only by a <c>template</c> tag (hotfix batch 9, Task 10) — the pre-approved
    /// template's exact name. Non-null is what marks this job as a template send rather
    /// than ordinary WhatsApp text/media, since both share <see cref="ChannelType.WP"/>.
    /// </summary>
    public string? TemplateName { get; set; }
    /// <summary>Ordered template variable values, in the order the tag listed them.</summary>
    public List<string>? TemplateVariables { get; set; }
}
