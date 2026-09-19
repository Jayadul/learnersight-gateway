using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface IGatewayCredentialService
{
    /// <summary>The current credentials with both keys masked.</summary>
    Task<GatewayCredentialDto> GetAsync();

    /// <summary>Applies only the fields that were actually supplied.</summary>
    Task<GatewayCredentialDto> UpdateAsync(UpdateGatewayCredentialRequest request);

    /// <summary>
    /// Syncs Username to the default SMS channel's Balance Client ID. Called by
    /// ChannelConfigService when the default SMS channel changes, or that channel's own
    /// Balance Client ID changes. A no-op when there is nothing to sync.
    /// </summary>
    Task SyncUsernameFromDefaultSmsChannelAsync(string? balanceClientId);
}
