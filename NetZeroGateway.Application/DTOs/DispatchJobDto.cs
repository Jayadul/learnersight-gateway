using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class DispatchJobDto
{
    public Guid Id { get; set; }
    public ChannelType ChannelType { get; set; }
    /// <summary>
    /// Name of the channel that actually sent this job. With several gateways active
    /// per type, the type alone does not say which one delivered — or failed.
    /// </summary>
    public string? ChannelLabel { get; set; }
    public string ToNumber { get; set; } = string.Empty;
    public string MessageContent { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ProviderRawResponse { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    /// <summary>Campaign dialled, on voice jobs only.</summary>
    public string? CampaignId { get; set; }
    public bool IsFallback { get; set; }
    public Guid? FallbackFromJobId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
