using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class ManualSendRequest
{
    public ChannelType ChannelType { get; set; }
    public string ToNumbers { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    /// <summary>Turns a WhatsApp send into a media send. Ignored on the other channels.</summary>
    public string? MediaUrl { get; set; }

    // ── Optional overrides. Each one null means "use the channel's configured value",
    // which is what the form sends when the admin leaves the field blank. ──

    /// <summary>Which channel of this type to send on. Null uses the type's default.</summary>
    public string? ChannelLabelSlug { get; set; }
    /// <summary>Sender id for this send. SMS only.</summary>
    public string? CallerId { get; set; }
    /// <summary>Campaign to dial. Voice only.</summary>
    public string? CampaignId { get; set; }
    /// <summary>Lot within the campaign. Voice only.</summary>
    public string? LotNumber { get; set; }
}
