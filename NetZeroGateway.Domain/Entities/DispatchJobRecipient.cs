using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Domain.Entities;

/// <summary>
/// One recipient of one <see cref="DispatchJob"/>, and whatever the provider later said
/// about that specific recipient.
/// <para>
/// SMS and Voice batch every recipient into a single job with a comma-joined
/// <see cref="DispatchJob.ToNumber"/>, so until now a job's outcome was all-or-nothing:
/// there was nowhere to record that four of five numbers arrived. WhatsApp already
/// creates one job per recipient, so these rows are a 1:1 wrapper there.
/// </para>
/// <para>
/// Providers differ in what they hand back. REVE returns one id for the whole batch, so
/// every row in a REVE job carries the same <see cref="ProviderReferenceId"/> and will
/// resolve to the same status — a provider limitation, not a gap here. Voice returns no
/// per-recipient id at all and is matched by number and time instead.
/// </para>
/// </summary>
public class DispatchJobRecipient : BaseEntity
{
    public Guid DispatchJobId { get; set; }

    /// <summary>Canonical digits-with-country-code, matching the stored request format.</summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>
    /// Whatever id the provider returned that identifies this recipient's message.
    /// Null when the provider gives none.
    /// </summary>
    public string? ProviderReferenceId { get; set; }

    public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.Pending;

    /// <summary>Latest raw status payload, kept verbatim so unrecognised states stay visible.</summary>
    public string? RawStatusResponse { get; set; }

    public DateTimeOffset? LastCheckedAt { get; set; }

    // ── Voice call timing (hotfix batch 9, Task 8) ──────────────────────────
    // From the combined report's StartTime/EndTime/ConnectTime — null for every non-Voice
    // channel, and ConnectTime stays null for a Voice call that never connected (busy,
    // no answer), since the provider itself never reports one for those.

    /// <summary>When the provider started dialling.</summary>
    public DateTimeOffset? CallStartTime { get; set; }

    /// <summary>When the call was actually answered — null if it never connected.</summary>
    public DateTimeOffset? CallConnectTime { get; set; }

    /// <summary>When the call ended.</summary>
    public DateTimeOffset? CallEndTime { get; set; }

    /// <summary>
    /// The recipient row this one was created to retry, when it came from a resend.
    /// A resend never mutates the row that failed: it creates a fresh job and fresh
    /// rows pointing back here, so the delivery log stays append-only and the original
    /// failure remains visible after a successful retry.
    /// </summary>
    public Guid? ResentFromRecipientId { get; set; }

    // Navigation
    public DispatchJob? DispatchJob { get; set; }
}
