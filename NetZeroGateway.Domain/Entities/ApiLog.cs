namespace NetZeroGateway.Domain.Entities;

public class ApiLog : BaseEntity
{
    public string? ApiKey { get; set; }
    public string? ToUser { get; set; }
    public string RawBody { get; set; } = string.Empty;
    public string ResponseBody { get; set; } = string.Empty;
    public string CallerIp { get; set; } = string.Empty;
    public int ResponseStatus { get; set; }
    public long DurationMs { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? MessageRequestId { get; set; }
}
