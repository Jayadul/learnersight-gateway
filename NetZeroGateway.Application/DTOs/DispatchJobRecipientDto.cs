using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;

/// <summary>
/// One recipient's outcome within a dispatch job. Sits alongside the job-level status
/// rather than replacing it: the job says what the provider answered when the message
/// was handed over, this says what happened to this number afterwards.
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
}
