using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;

/// <summary>
/// One channel's cached balance, in a shape that needs no per-vendor rendering.
/// </summary>
public class ChannelBalanceDto
{
    public Guid ChannelConfigId { get; set; }
    public string Label { get; set; } = string.Empty;
    public ChannelType ChannelType { get; set; }
    public Provider Provider { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    /// <summary>Remaining balance. Null when it has never been read successfully.</summary>
    public decimal? Available { get; set; }

    /// <summary>
    /// Credit limit where the provider reports one. Null means the provider has no such
    /// concept — render it as blank, not as zero.
    /// </summary>
    public decimal? CreditLimit { get; set; }

    /// <summary>When the figure above was last read successfully.</summary>
    public DateTimeOffset? CheckedAt { get; set; }

    /// <summary>Why the last refresh failed, or null if it succeeded.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// A figure is showing but the most recent refresh failed, so it is the last known
    /// good value rather than a current one. The card keeps showing it with a marker
    /// instead of blanking, which would read as "no balance".
    /// </summary>
    public bool IsStale => Error is not null && Available is not null;
}
