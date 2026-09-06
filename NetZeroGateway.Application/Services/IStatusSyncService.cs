using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface IStatusSyncService
{
    /// <summary>Which channels can be synced, for the admin UI to offer the action.</summary>
    Task<List<StatusSyncResultDto>> GetSyncableChannelsAsync();

    /// <summary>Syncs one channel over an explicit window, or the default one.</summary>
    Task<StatusSyncResultDto> SyncChannelAsync(Guid channelConfigId, StatusSyncRequest request);

    /// <summary>
    /// The daily job body: every sync-capable active channel over the previous full day.
    /// Per-channel try/catch so one unreachable provider cannot stop the batch.
    /// </summary>
    Task SyncAllAsync();
}
