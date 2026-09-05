using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.Strategies;

namespace NetZeroGateway.Tests;

/// <summary>
/// Sender resolution now keys on (ChannelType, Provider, HandlesMedia). Before Release 3
/// a channel type implied its vendor, so these tests pin the behaviour that lets two
/// providers serve one type without either stealing the other's traffic. See ADR 0008.
/// </summary>
public class ChannelSenderFactoryTests
{
    private sealed class StubSender(
        ChannelType channelType, Provider provider, bool handlesMedia = false) : IChannelSender
    {
        public ChannelType ChannelType { get; } = channelType;
        public Provider Provider { get; } = provider;
        public bool HandlesMedia { get; } = handlesMedia;

        public Task SendAsync(
            DispatchJob job, Dictionary<string, string> config, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    [Fact]
    public void Two_providers_on_one_channel_type_resolve_to_their_own_sender()
    {
        var reve = new StubSender(ChannelType.SMS, Provider.Reve);
        var genNet = new StubSender(ChannelType.SMS, Provider.GenNet);
        var factory = new ChannelSenderFactory([reve, genNet]);

        Assert.Same(reve, factory.GetSender(ChannelType.SMS, Provider.Reve));
        Assert.Same(genNet, factory.GetSender(ChannelType.SMS, Provider.GenNet));
    }

    [Fact]
    public void Media_flag_picks_between_two_senders_of_the_same_provider()
    {
        var text = new StubSender(ChannelType.WP, Provider.WaTrend);
        var media = new StubSender(ChannelType.WP, Provider.WaTrend, handlesMedia: true);
        var factory = new ChannelSenderFactory([text, media]);

        Assert.Same(text, factory.GetSender(ChannelType.WP, Provider.WaTrend));
        Assert.Same(media, factory.GetSender(ChannelType.WP, Provider.WaTrend, hasMedia: true));
    }

    [Fact]
    public void A_media_job_falls_back_to_the_text_sender_when_the_provider_has_only_one()
    {
        // SMS and Voice register a single sender; a job that happens to carry a MediaUrl
        // must still dispatch rather than fail to resolve.
        var sms = new StubSender(ChannelType.SMS, Provider.Reve);
        var factory = new ChannelSenderFactory([sms]);

        Assert.Same(sms, factory.GetSender(ChannelType.SMS, Provider.Reve, hasMedia: true));
    }

    [Fact]
    public void An_unregistered_provider_throws_rather_than_substituting_another_vendor()
    {
        // The whole point of the provider key: a GenNet channel must never quietly
        // dispatch through REVE and bill the wrong account.
        var factory = new ChannelSenderFactory([new StubSender(ChannelType.SMS, Provider.Reve)]);

        var ex = Assert.Throws<InvalidOperationException>(
            () => factory.GetSender(ChannelType.SMS, Provider.GenNet));

        Assert.Contains("GenNet", ex.Message);
    }

    [Fact]
    public void Two_senders_claiming_the_same_key_fail_at_construction()
    {
        // Which one won would otherwise depend on DI registration order.
        var ex = Assert.Throws<InvalidOperationException>(() => new ChannelSenderFactory([
            new StubSender(ChannelType.SMS, Provider.Reve),
            new StubSender(ChannelType.SMS, Provider.Reve)]));

        Assert.Contains("Two IChannelSender implementations", ex.Message);
    }
}
