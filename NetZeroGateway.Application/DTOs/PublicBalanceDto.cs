namespace NetZeroGateway.Application.DTOs;

/// <summary>
/// The response shape of the public, unauthenticated <c>GET /balance</c> endpoint — a
/// third-party integrator's view, not the admin dashboard's. Deliberately smaller than
/// <see cref="ChannelBalanceDto"/>: no channel id, provider, or active flag, since an
/// unauthenticated caller has no business seeing internal identifiers.
/// </summary>
public class PublicBalanceDto
{
    public string Label { get; set; } = string.Empty;

    /// <summary>Cached balance. Never a live provider call — see ChannelBalanceService.</summary>
    public decimal? Balance { get; set; }

    /// <summary>Null means the provider has no such concept, never zero.</summary>
    public decimal? CreditLimit { get; set; }

    public DateTimeOffset? LastCheckedAt { get; set; }
}
