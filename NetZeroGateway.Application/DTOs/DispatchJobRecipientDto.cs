using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;

/// <summary>
/// One recipient's outcome within a dispatch job. Sits alongside the job-level status
/// rather than replacing it: the job says what the provider answered when the message
/// was handed over, this says what happened to this number afterwards.
/// <para>
/// Since hotfix batch 3 this is also the row shape for the merged, per-recipient Message
/// Logs page — it carries everything that page and its detail view need (channel, content,
/// dates) so no second call is needed to show a recipient's own record.
/// </para>
/// </summary>
public class DispatchJobRecipientDto
{
    public Guid Id { get; set; }
    public Guid DispatchJobId { get; set; }
    public string Recipient { get; set; } = string.Empty;
    /// <summary>Provider-side id for this recipient; null when the provider returns none.</summary>
    public string? ProviderReferenceId { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ChannelType ChannelType { get; set; }
    /// <summary>Name of the channel that actually sent this job.</summary>
    public string? ChannelLabel { get; set; }
    public string MessageContent { get; set; } = string.Empty;
    /// <summary>Campaign dialled, on voice jobs only.</summary>
    public string? CampaignId { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public bool IsFallback { get; set; }
    /// <summary>The row this one was created to retry, when it came from a resend.</summary>
    public Guid? ResentFromRecipientId { get; set; }

    /// <summary>
    /// The exact raw provider response for this recipient's own send — this row's own
    /// <c>RawStatusResponse</c> once a status sync has run, falling back to the job-level
    /// response (populated immediately at dispatch time) until then, so the tooltip is
    /// never blank just because sync hasn't reached this row yet. For a batched Chithi job
    /// this is this recipient's own entry pulled out of the job's per-call array, not the
    /// whole batch.
    /// </summary>
    public string? RawProviderResponse { get; set; }
}
