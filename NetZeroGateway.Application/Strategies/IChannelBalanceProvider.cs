using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Strategies;

/// <summary>
/// Reads one provider's remaining balance. Separate from <see cref="IChannelSender"/>
/// because balance and sending are independent capabilities: Voice and WhatsApp channels
/// have no balance endpoint, and REVE's balance sits on a different host from its send
/// endpoint. A channel type is never assumed to have one.
/// </summary>
public interface IChannelBalanceProvider
{
    Provider Provider { get; }

    /// <summary>
    /// Reads the balance using the channel's decrypted config. Never throws — a provider
    /// being down must leave the last known figure in place, not fail the page that
    /// shows it.
    /// </summary>
    Task<ChannelBalanceResult> GetBalanceAsync(
        Dictionary<string, string> config, CancellationToken ct = default);
}

/// <summary>
/// A balance reading, in a shape every provider maps into so the dashboard needs no
/// per-vendor rendering. <paramref name="CreditLimit"/> is null where the provider has
/// no such concept — which is not the same as a limit of zero.
/// </summary>
public record ChannelBalanceResult(
    bool Success,
    decimal? Available,
    decimal? CreditLimit,
    string? Error)
{
    public static ChannelBalanceResult Ok(decimal available, decimal? creditLimit = null)
        => new(true, available, creditLimit, null);

    public static ChannelBalanceResult Failed(string error)
        => new(false, null, null, error);
}
