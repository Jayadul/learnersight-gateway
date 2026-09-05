using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Infrastructure.Strategies;

public class ChannelSenderFactory : IChannelSenderFactory
{
    // Keyed on (ChannelType, Provider, HandlesMedia). The provider joined the key in
    // Release 3: several vendors now serve one channel type, so the type and media flag
    // together no longer identify a sender.
    private readonly Dictionary<(ChannelType, Provider, bool), IChannelSender> _map;

    public ChannelSenderFactory(IEnumerable<IChannelSender> senders)
    {
        _map = [];
        foreach (var sender in senders)
        {
            var key = (sender.ChannelType, sender.Provider, sender.HandlesMedia);
            // A duplicate registration means two senders claim the same key and which one
            // wins would depend on DI ordering. Fail at startup rather than silently
            // dispatching through whichever happened to register last.
            if (!_map.TryAdd(key, sender))
            {
                throw new InvalidOperationException(
                    $"Two IChannelSender implementations are registered for "
                    + $"({sender.ChannelType}, {sender.Provider}, hasMedia={sender.HandlesMedia}): "
                    + $"'{_map[key].GetType().Name}' and '{sender.GetType().Name}'. "
                    + "Each combination must have exactly one sender.");
            }
        }
    }

    public IChannelSender GetSender(ChannelType channelType, Provider provider, bool hasMedia = false)
    {
        if (_map.TryGetValue((channelType, provider, hasMedia), out var sender)) return sender;

        // Providers with a single sender (SMS, Voice) register with HandlesMedia = false;
        // fall back to that rather than failing when a job happens to carry a MediaUrl.
        if (hasMedia && _map.TryGetValue((channelType, provider, false), out var textSender))
            return textSender;

        throw new InvalidOperationException(
            $"No IChannelSender registered for ChannelType '{channelType}' with provider "
            + $"'{provider}' (hasMedia={hasMedia}). Either the channel config names a provider "
            + "that has no sender yet, or a new sender needs registering in DependencyInjection.");
    }
}
