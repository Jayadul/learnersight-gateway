using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Strategies;

/// <summary>
/// Factory that returns the correct IChannelSender for a given ChannelType.
/// Registered as scoped; all senders injected at startup.
/// </summary>
public interface IChannelSenderFactory
{
    IChannelSender GetSender(ChannelType channelType);
}
