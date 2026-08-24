using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Interfaces;

public interface IChannelConfigRepository : IRepository<ChannelConfig>
{
    /// <summary>
    /// The default config for a type — where untagged and unlabelled sends resolve.
    /// Replaces the old "the active one": several configs of a type may now be active.
    /// </summary>
    Task<ChannelConfig?> GetDefaultByChannelTypeAsync(ChannelType type);

    /// <summary>Resolves a labelled send (<c>[sms_label:slug::…]</c>) to an active config.</summary>
    Task<ChannelConfig?> GetActiveBySlugAsync(ChannelType type, string labelSlug);

    Task<List<ChannelConfig>> GetAllByChannelTypeAsync(ChannelType type);

    /// <summary>Makes one config the type's default, clearing the flag on its siblings.</summary>
    Task SetDefaultAsync(Guid id, ChannelType type);
}
