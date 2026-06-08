using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Domain.Entities;

public class MessageRequest : BaseEntity
{
    public string        ApiKey             { get; set; } = string.Empty;
    public string        CallerID           { get; set; } = string.Empty;
    /// <summary>Comma-separated as received from x-soft.</summary>
    public string        ToUser             { get; set; } = string.Empty;
    public string        RawMessageContent  { get; set; } = string.Empty;
    public List<string>  ParsedChannels     { get; set; } = new();
    public MessageStatus OverallStatus      { get; set; } = MessageStatus.Pending;
    public DateTimeOffset ReceivedAt        { get; set; } = DateTimeOffset.UtcNow;
    public MessageSource Source             { get; set; }

    // Navigation
    public ICollection<DispatchJob> Jobs { get; set; } = new List<DispatchJob>();
}
