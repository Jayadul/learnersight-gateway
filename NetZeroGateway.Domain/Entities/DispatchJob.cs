using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Domain.Entities;

public class DispatchJob : BaseEntity
{
    public Guid MessageRequestId { get; set; }
    public ChannelType ChannelType { get; set; }
    public Guid ChannelConfigId { get; set; }
    public string MessageContent { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    /// <summary>
    /// The campaign the sender asked for, as written in the tag. Never overwritten with a
    /// provider-side id — that goes in <see cref="ProviderMessageId"/>, so the log can
    /// still be read back against what was requested.
    /// </summary>
    public string? CampaignId { get; set; }
    /// <summary>
    /// Recipients for this job. One number for WhatsApp, which dispatches per recipient;
    /// a comma-joined list for SMS and Voice, which batch every recipient into one call.
    /// <see cref="Recipients"/> holds the same numbers one row apiece.
    /// </summary>
    public string ToNumber { get; set; } = string.Empty;
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public string? ProviderMessageId { get; set; }
    public string? ProviderRawResponse { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public bool IsFallback { get; set; } = false;
    public Guid? FallbackFromJobId { get; set; }

    // Navigation
    public MessageRequest? MessageRequest { get; set; }
    public ChannelConfig? ChannelConfig { get; set; }
    public ICollection<DispatchJobRecipient> Recipients { get; set; } = [];
}
