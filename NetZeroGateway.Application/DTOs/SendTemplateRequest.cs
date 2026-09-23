namespace NetZeroGateway.Application.DTOs;

/// <summary>
/// Admin-initiated WhatsApp Template send (hotfix batch 3) — a distinct action from
/// ManualSendRequest, since a template is a structured payload (name + ordered variables)
/// rather than a free-text message and does not go through the dispatch/fallback pipeline.
/// </summary>
public class SendTemplateRequest
{
    /// <summary>Must name an active, WhatsApp, NetZero-Inbox-provider channel with a Template API Token configured.</summary>
    public string ChannelLabelSlug { get; set; } = string.Empty;
    public string ToNumber { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    /// <summary>Ordered template variable values — count varies per template.</summary>
    public List<string> ExampleArr { get; set; } = [];
    public string? MediaUrl { get; set; }
    /// <summary>Blank uses the channel's configured Template API Token; filled overrides it for this send only.</summary>
    public string? Token { get; set; }
}
