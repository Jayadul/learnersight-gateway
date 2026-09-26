using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

public class DeliveryStatusRulesTests
{
    [Fact]
    public void A_whatsapp_send_the_provider_accepted_is_delivered_at_once()
        => Assert.Equal(DeliveryStatus.Delivered,
            DeliveryStatusRules.InitialFor(ChannelType.WP, JobStatus.Sent));

    [Fact]
    public void A_whatsapp_send_the_provider_refused_is_failed()
        => Assert.Equal(DeliveryStatus.Failed,
            DeliveryStatusRules.InitialFor(ChannelType.WP, JobStatus.Failed));

    [Theory]
    [InlineData(ChannelType.SMS)]
    [InlineData(ChannelType.Voice)]
    public void Sms_and_voice_wait_for_the_sync_because_their_providers_report_later(ChannelType channel)
        => Assert.Equal(DeliveryStatus.Pending, DeliveryStatusRules.InitialFor(channel, JobStatus.Sent));

    [Theory]
    [InlineData(ChannelType.SMS)]
    [InlineData(ChannelType.Voice)]
    public void A_failed_sms_or_voice_send_starts_failed(ChannelType channel)
        => Assert.Equal(DeliveryStatus.Failed, DeliveryStatusRules.InitialFor(channel, JobStatus.Failed));
}
