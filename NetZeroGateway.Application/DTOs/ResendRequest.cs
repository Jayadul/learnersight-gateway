namespace NetZeroGateway.Application.DTOs;

/// <summary>
/// Resends specific recipients. Recipient-level rather than job-level on purpose: SMS
/// and Voice batch many numbers into one job, and resending the job would re-send to
/// everyone including those it already reached.
/// </summary>
public class ResendRequest
{
    /// <summary>The dispatch_job_recipients rows to retry.</summary>
    public List<Guid> RecipientIds { get; set; } = [];

    /// <summary>
    /// Send through a different channel instead of the one that failed. Must be an
    /// active channel of the same type — crossing types is a materially different
    /// feature (no guaranteed WhatsApp number for an SMS-only contact) and is refused.
    /// Omit to retry on the original channel.
    /// </summary>
    public Guid? ChannelConfigId { get; set; }
}
