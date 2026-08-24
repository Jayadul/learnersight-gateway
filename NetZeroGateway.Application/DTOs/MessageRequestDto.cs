using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class MessageRequestDto
{
    public Guid Id { get; set; }
    public string ToUser { get; set; } = string.Empty;
    /// <summary>The message as submitted, tags and all. Truncated for display by the UI.</summary>
    public string RawMessageContent { get; set; } = string.Empty;
    public List<string> ParsedChannels { get; set; } = [];
    public MessageStatus OverallStatus { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public MessageSource Source { get; set; }
    /// <summary>The sender tagged more than one delivery, so fallback does not apply.</summary>
    public bool IsMultiChannel { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
