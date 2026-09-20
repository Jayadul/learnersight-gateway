using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public interface IChannelConfigService
{
    Task<List<ChannelConfigDto>> GetAllAsync(ChannelType? filter);
    Task<ChannelConfigDto> GetByIdAsync(Guid id);
    Task<ChannelConfigDto> CreateAsync(CreateChannelConfigRequest request);
    Task<ChannelConfigDto> UpdateAsync(Guid id, UpdateChannelConfigRequest request);
    Task DeleteAsync(Guid id);
    /// <summary>Toggles availability; independent of which config is the default.</summary>
    Task SetActiveAsync(Guid id, bool isActive);
    /// <summary>Promotes one config to be its type's default, demoting the previous one.</summary>
    Task SetDefaultAsync(Guid id);

    /// <summary>
    /// The real value of one config field, decrypted — for the reveal-on-click action on
    /// an otherwise partially-masked secret. Scoped to a single named field rather than
    /// the whole config, so revealing one credential never hands back every other one at
    /// the same time.
    /// </summary>
    Task<string> RevealFieldAsync(Guid id, string field);

    /// <summary>
    /// The minimal, non-sensitive channel list (id/label/labelSlug/default) for pages
    /// that are not SuperAdmin-restricted but still need to know which gateways exist —
    /// exposed through a different controller (see <see cref="ChannelOptionDto"/>) so
    /// this service's own SuperAdmin-only surface is untouched.
    /// </summary>
    Task<List<ChannelOptionDto>> GetOptionsAsync(ChannelType? filter);
}
