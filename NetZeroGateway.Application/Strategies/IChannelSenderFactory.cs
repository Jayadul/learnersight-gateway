using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Strategies;

/// <summary>
/// Factory that returns the correct IChannelSender for a given ChannelType.
/// Registered as scoped; all senders injected at startup.
/// </summary>
public interface IChannelSenderFactory
{
    /// <summary>
    /// Resolves the sender for a channel. <paramref name="hasMedia"/> disambiguates
    /// WhatsApp text from WhatsApp media, which share a single ChannelType; channels
    /// with one sender ignore it.
    /// </summary>
    IChannelSender GetSender(ChannelType channelType, bool hasMedia = false);
}
