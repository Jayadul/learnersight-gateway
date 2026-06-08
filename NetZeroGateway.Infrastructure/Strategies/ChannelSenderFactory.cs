using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Infrastructure.Strategies;

public class ChannelSenderFactory(IEnumerable<IChannelSender> senders) : IChannelSenderFactory
{
    private readonly Dictionary<ChannelType, IChannelSender> _map =
        senders.ToDictionary(s => s.ChannelType);

    public IChannelSender GetSender(ChannelType channelType)
    {
        if (_map.TryGetValue(channelType, out var sender)) return sender;
        throw new InvalidOperationException(
            $"No IChannelSender registered for ChannelType '{channelType}'. " +
            $"Add a concrete sender and register it in DependencyInjection.");
    }
}
