using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Infrastructure.Strategies;

public class ChannelSenderFactory(IEnumerable<IChannelSender> senders) : IChannelSenderFactory
{
    // Keyed on (ChannelType, HandlesMedia): WhatsApp text and media share ChannelType.WP,
    // so the type alone is no longer unique.
    private readonly Dictionary<(ChannelType, bool), IChannelSender> _map =
        senders.ToDictionary(s => (s.ChannelType, s.HandlesMedia));

    public IChannelSender GetSender(ChannelType channelType, bool hasMedia = false)
    {
        if (_map.TryGetValue((channelType, hasMedia), out var sender)) return sender;

        // Channels with a single sender (SMS, Voice) register with HandlesMedia = false;
        // fall back to that rather than failing when a job happens to carry a MediaUrl.
        if (hasMedia && _map.TryGetValue((channelType, false), out var textSender))
            return textSender;

        throw new InvalidOperationException(
            $"No IChannelSender registered for ChannelType '{channelType}' (hasMedia={hasMedia}). " +
            $"Add a concrete sender and register it in DependencyInjection.");
    }
}
