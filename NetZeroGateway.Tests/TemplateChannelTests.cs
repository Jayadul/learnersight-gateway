using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.Strategies;

namespace NetZeroGateway.Tests;

/// <summary>
/// The WhatsApp Template channel (hotfix batch 6): a first-class provider of its own, so it is
/// named and picked as a template channel, and the ordinary dispatch pipeline refuses it.
/// </summary>
public class TemplateChannelTests
{
    [Fact]
    public async Task A_text_send_to_a_template_channel_fails_with_a_reason_that_says_so()
    {
        var sender = new NetZeroInboxTemplateOnlySender(NullLogger<NetZeroInboxTemplateOnlySender>.Instance);
        var job = new DispatchJob { ToNumber = "8801748848039", MessageContent = "hi", Status = JobStatus.Pending };

        await sender.SendAsync(job, []);

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Contains("Template", job.FailureReason);
        Assert.Contains("Send Template", job.FailureReason);
    }

    [Fact]
    public void The_template_sender_is_keyed_to_whatsapp_and_its_own_provider()
    {
        var sender = new NetZeroInboxTemplateOnlySender(NullLogger<NetZeroInboxTemplateOnlySender>.Instance);

        Assert.Equal(ChannelType.WP, sender.ChannelType);
        Assert.Equal(Provider.NetZeroInboxTemplate, sender.Provider);
        Assert.False(((NetZeroGateway.Application.Strategies.IChannelSender)sender).HandlesMedia);
    }

    [Fact]
    public void The_template_provider_takes_the_recipient_with_a_leading_plus()
        => Assert.Equal("+8801748848039", RecipientFormat.For(Provider.NetZeroInboxTemplate, "8801748848039"));

    [Fact]
    public void The_provider_value_is_stable_on_the_wire()
        => Assert.Equal(7, (int)Provider.NetZeroInboxTemplate);
}
