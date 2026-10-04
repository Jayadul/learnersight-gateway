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

    /// <summary>
    /// The gateway-level outcome — "ACCEPTD" or "REJECTD", the response body's own
    /// <c>Text</c> field (hotfix batch 9, Task 12). Every /sendtext call returns HTTP 200
    /// regardless of outcome (the REVE-compatible contract carries its real status inside
    /// the JSON body, not the status line), so <see cref="ResponseStatus"/> alone cannot
    /// tell a rejected request apart from an accepted one — this is what actually makes a
    /// failed request findable and filterable. Null on a row written before this column
    /// existed, or if the response body was ever something other than the gateway shape.
    /// </summary>
    public string? GatewayResult { get; set; }
}
