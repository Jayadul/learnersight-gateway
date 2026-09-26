using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Utilities;

/// <summary>
/// Where a recipient row starts once its send has been made.
/// <para>
/// WhatsApp providers have no delivery lookup, so a row left Pending would wait forever for a
/// sync that never runs for that channel type. WhatsApp therefore resolves straight from the
/// provider's immediate send response: accepted is Delivered, refused is Failed. SMS and Voice
/// stay Pending, because REVE, Chithi and NetZero Voice each answer later through the sync.
/// </para>
/// </summary>
public static class DeliveryStatusRules
{
    public static DeliveryStatus InitialFor(ChannelType channel, JobStatus jobStatus)
        => jobStatus == JobStatus.Failed ? DeliveryStatus.Failed
         : channel == ChannelType.WP ? DeliveryStatus.Delivered
         : DeliveryStatus.Pending;
}
