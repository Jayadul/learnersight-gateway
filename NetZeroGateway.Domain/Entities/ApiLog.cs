namespace NetZeroGateway.Domain.Entities;

public class ApiLog : BaseEntity
{
    public string? ToUser { get; set; }
    public string RawBody { get; set; } = string.Empty;
    public string ResponseBody { get; set; } = string.Empty;
    public string CallerIp { get; set; } = string.Empty;
    public int ResponseStatus { get; set; }
    public long DurationMs { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? MessageRequestId { get; set; }

    /// <summary>
    /// Channel types the request's tags asked for, comma-joined ("SMS, WhatsApp"). A
    /// snapshot rather than a reference: an API log is a record of what was asked for, and
    /// it must stay readable after a channel is renamed or deleted. Null on rows written
    /// before this column existed, and on requests rejected before parsing.
    /// </summary>
    public string? RequestedChannels { get; set; }
}
