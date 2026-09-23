using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Domain.Entities;

/// <summary>
/// One resend attempt's own record (hotfix batch 4, task 2a — a deliberate reversal of
/// the earlier "every resend is a new linked row" design). Resend used to create a new
/// <see cref="DispatchJobRecipient"/> row per attempt, so a recipient retried three times
/// showed as three separate rows in Message Logs — confusing, and reading as three
/// different sends. The recipient row an admin sees is now updated in place across
/// attempts; this table keeps each attempt's own outcome and raw response recoverable
/// (in the row's detail view) without cluttering the main list with one row per try.
/// </summary>
public class ResendAttempt : BaseEntity
{
    public Guid DispatchJobRecipientId { get; set; }
    public Guid DispatchJobId { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; }
    public string? ProviderReferenceId { get; set; }
    /// <summary>The exact raw provider response for this one attempt, kept verbatim.</summary>
    public string? RawResponse { get; set; }
    public DateTimeOffset AttemptedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public DispatchJobRecipient? DispatchJobRecipient { get; set; }
    public DispatchJob? DispatchJob { get; set; }
}
