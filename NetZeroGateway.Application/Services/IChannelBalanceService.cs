using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface IChannelBalanceService
{
    /// <summary>
    /// Cached balances for every channel whose provider supports one. Reads the database
    /// only — never calls a provider, so a slow vendor cannot stall the dashboard.
    /// </summary>
    Task<List<ChannelBalanceDto>> GetAllAsync();

    /// <summary>Refreshes one channel from its provider and returns the new figure.</summary>
    Task<ChannelBalanceDto> RefreshAsync(Guid channelConfigId);

    /// <summary>
    /// Refreshes every balance-capable channel. The Hangfire job body — per-channel
    /// try/catch so one bad provider cannot stop the batch.
    /// </summary>
    Task RefreshAllAsync();

    /// <summary>
    /// Resolves a label to an active, balance-capable channel for the public
    /// <c>GET /balance</c> endpoint, then reads that channel's balance live from its
    /// provider (team-lead-approved reversal of the original cached-only design — this
    /// endpoint has no authentication, so there is no rate limiting on how often that
    /// live call can be triggered). A failed live read still returns the last known
    /// cached figure rather than an error. Null when the label is unknown, the channel is
    /// inactive, or its provider does not report a balance — the controller turns that
    /// into one generic error either way, so an unauthenticated caller cannot use this to
    /// probe which labels exist.
    /// </summary>
    Task<PublicBalanceDto?> GetByLabelAsync(string label);
}
