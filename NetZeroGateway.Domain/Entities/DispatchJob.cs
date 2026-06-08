using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Domain.Entities;

public class DispatchJob : BaseEntity
{
    public Guid        MessageRequestId    { get; set; }
    public ChannelType ChannelType         { get; set; }
    public Guid        ChannelConfigId     { get; set; }
    public string      MessageContent      { get; set; } = string.Empty;
    public string?     MediaUrl            { get; set; }
    public string?     CampaignId          { get; set; }
    /// <summary>Single phone number this job targets.</summary>
    public string      ToNumber            { get; set; } = string.Empty;
    public JobStatus   Status              { get; set; } = JobStatus.Pending;
    public string?     ProviderMessageId   { get; set; }
    public string?     ProviderRawResponse { get; set; }
    public string?     FailureReason       { get; set; }
    public DateTimeOffset? SentAt          { get; set; }
    public bool        IsFallback          { get; set; } = false;
    public Guid?       FallbackFromJobId   { get; set; }

    // Navigation
    public MessageRequest? MessageRequest  { get; set; }
    public ChannelConfig?  ChannelConfig   { get; set; }
}
