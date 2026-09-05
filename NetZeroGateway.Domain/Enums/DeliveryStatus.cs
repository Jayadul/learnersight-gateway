namespace NetZeroGateway.Domain.Enums;

/// <summary>
/// Per-recipient delivery outcome, synced back from the provider after a send.
/// Deliberately not <see cref="JobStatus"/>: a job's status records what the provider
/// said when we handed the message over, while this records what happened to one
/// recipient afterwards. A <c>Sent</c> job can hold a mix of Delivered and Failed
/// recipients, so collapsing the two would lose exactly the detail this table exists for.
/// </summary>
public enum DeliveryStatus
{
    /// <summary>Accepted by the provider; no status has been synced back yet.</summary>
    Pending = 0,

    /// <summary>The provider confirmed delivery to this recipient.</summary>
    Delivered = 1,

    /// <summary>The provider reported a delivery failure for this recipient.</summary>
    Failed = 2,

    /// <summary>
    /// No usable status. Either the provider offers no status endpoint, or it answered
    /// with a state we do not recognise — never guess Failed from an unknown state, or
    /// a working send starts reading as a broken one.
    /// </summary>
    Unknown = 3
}
