using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Strategies;

/// <summary>
/// Factory that returns the correct IChannelSender for a channel config.
/// Registered as scoped; all senders injected at startup.
/// </summary>
public interface IChannelSenderFactory
{
    /// <summary>
    /// Resolves the sender for a channel. <paramref name="provider"/> picks between
    /// vendors serving the same channel type; <paramref name="hasMedia"/> disambiguates
    /// WhatsApp text from WhatsApp media. Throws when no sender is registered for the
    /// combination — a channel config naming a provider we cannot send through is a
    /// configuration error, not something to paper over with a substitute vendor.
    /// </summary>
    IChannelSender GetSender(ChannelType channelType, Provider provider, bool hasMedia = false);
}
