using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class DispatchJobDto
{
    public Guid Id { get; set; }
    public ChannelType ChannelType { get; set; }
    public string ToNumber { get; set; } = string.Empty;
    public string MessageContent { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ProviderRawResponse { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public bool IsFallback { get; set; }
    public Guid? FallbackFromJobId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
