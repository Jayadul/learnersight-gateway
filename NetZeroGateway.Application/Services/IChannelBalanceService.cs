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
}
