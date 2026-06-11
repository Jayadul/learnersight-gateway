using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;

namespace NetZeroGateway.Infrastructure.Strategies;

public class WhatsAppTextSender(
    IWaTrendClient client,
    ILogger<WhatsAppTextSender> logger
) : IChannelSender
{
    public ChannelType ChannelType => ChannelType.WP_Text;

    public async Task SendAsync(
        DispatchJob job, Dictionary<string, string> cfg, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation(
                "[WA_TEXT] Sending to {ToNumber} | RequestId={RequestId}",
                job.ToNumber, job.MessageRequestId);

            var r = await client.SendTextAsync(
                cfg["instance_id"], cfg["access_token"], job.ToNumber, job.MessageContent, ct);

            job.ProviderRawResponse = JsonSerializer.Serialize(r);

            if (r.IsSuccess)
            {
                job.Status = JobStatus.Sent;
                job.SentAt = DateTimeOffset.UtcNow;
                logger.LogInformation("[WA_TEXT] Sent OK to {ToNumber}", job.ToNumber);
            }
            else
            {
                job.Status = JobStatus.Failed;
                job.FailureReason = r.Status;
                logger.LogWarning(
                    "[WA_TEXT] Failed for {ToNumber} | Status={Status}", job.ToNumber, r.Status);
            }
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.FailureReason = ex.Message;
            logger.LogError(ex, "[WA_TEXT] Exception dispatching to {ToNumber}", job.ToNumber);
        }
    }
}
