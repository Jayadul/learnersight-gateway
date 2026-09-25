using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Infrastructure.Strategies;

/// <summary>
/// The dispatch-pipeline face of a Template channel: it does not send. Templates are sent
/// through <see cref="IWhatsAppTemplateSender"/> from Manual Send; this exists so a text or
/// media tag that names a template channel fails with a plain reason instead of a missing
/// sender exception.
/// </summary>
public class NetZeroInboxTemplateOnlySender(ILogger<NetZeroInboxTemplateOnlySender> logger) : IChannelSender
{
    public ChannelType ChannelType => ChannelType.WP;
    public Provider Provider => Provider.NetZeroInboxTemplate;

    public Task SendAsync(DispatchJob job, Dictionary<string, string> cfg, CancellationToken ct = default)
    {
        job.Status = JobStatus.Failed;
        job.FailureReason =
            "This is a WhatsApp Template channel: it only sends templates, from Manual Send "
            + "under Send Template. Use a regular WhatsApp channel for text or media.";
        logger.LogWarning(
            "[NZ_INBOX_TEMPLATE] Text/media send to {To} refused: template-only channel", job.ToNumber);
        return Task.CompletedTask;
    }
}
