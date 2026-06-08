using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;

namespace NetZeroGateway.Infrastructure.Strategies;

public class WhatsAppMediaSender(
    IWaTrendClient               client,
    ILogger<WhatsAppMediaSender> logger
) : IChannelSender
{
    public ChannelType ChannelType => ChannelType.WP_Media;

    public async Task SendAsync(
        DispatchJob job, Dictionary<string, string> cfg, CancellationToken ct = default)
    {
        try
        {
            var mediaUrl = job.MediaUrl ?? "";
            logger.LogInformation(
                "[WA_MEDIA] Sending to {ToNumber} | MediaUrl={Url} | RequestId={RequestId}",
                job.ToNumber, mediaUrl, job.MessageRequestId);

            var r = await client.SendMediaAsync(
                cfg["instance_id"], cfg["access_token"],
                job.ToNumber, job.MessageContent, mediaUrl, ct);

            job.ProviderRawResponse = JsonSerializer.Serialize(r);

            if (r.IsSuccess)
            {
                job.Status = JobStatus.Sent;
                job.SentAt = DateTimeOffset.UtcNow;
                logger.LogInformation("[WA_MEDIA] Sent OK to {ToNumber}", job.ToNumber);
            }
            else
            {
                job.Status        = JobStatus.Failed;
                job.FailureReason = r.Status;
                logger.LogWarning(
                    "[WA_MEDIA] Failed for {ToNumber} | Status={Status}", job.ToNumber, r.Status);
            }
        }
        catch (Exception ex)
        {
            job.Status        = JobStatus.Failed;
            job.FailureReason = ex.Message;
            logger.LogError(ex, "[WA_MEDIA] Exception dispatching to {ToNumber}", job.ToNumber);
        }
    }
}
