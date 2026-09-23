using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.DTOs;

/// <summary>
/// One resend attempt's own record (hotfix batch 4, task 2a). Message Logs' main table
/// only ever shows the recipient's current row; this is what the detail view's history
/// section reads to recover what happened on an earlier attempt.
/// </summary>
public class ResendAttemptDto
{
    public Guid Id { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; }
    public string? ProviderReferenceId { get; set; }
    public string? RawResponse { get; set; }
    public string? ChannelLabel { get; set; }
    public DateTimeOffset AttemptedAt { get; set; }
}
