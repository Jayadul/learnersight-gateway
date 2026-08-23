using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Domain.Entities;

public class MessageRequest : BaseEntity
{
    public string CallerID { get; set; } = string.Empty;
    public string ToUser { get; set; } = string.Empty;
    public string RawMessageContent { get; set; } = string.Empty;
    public List<string> ParsedChannels { get; set; } = new();
    public MessageStatus OverallStatus { get; set; } = MessageStatus.Pending;
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public MessageSource Source { get; set; }

    /// <summary>
    /// True when the sender's tags asked for more than one delivery, so fallback must
    /// not add another. Set once at parse time — counting sibling jobs later would give
    /// a different answer after a retry or a cleanup.
    /// </summary>
    public bool IsMultiChannel { get; set; }

    // Navigation
    public ICollection<DispatchJob> Jobs { get; set; } = new List<DispatchJob>();
}
