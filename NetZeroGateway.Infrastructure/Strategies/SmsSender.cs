using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;

namespace NetZeroGateway.Infrastructure.Strategies;

public class SmsSender(
    IReveSmsClient client,
    ILogger<SmsSender> logger
) : IChannelSender
{
    public ChannelType ChannelType => ChannelType.SMS;

    public async Task SendAsync(
        DispatchJob job, Dictionary<string, string> cfg, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation(
                "[SMS] Sending to {ToNumber} | RequestId={RequestId}",
                job.ToNumber, job.MessageRequestId);

            var r = await client.SendAsync(
                cfg["apikey"], cfg["secretkey"],
                cfg.GetValueOrDefault("callerID", ""),
                job.ToNumber, job.MessageContent, ct);

            job.ProviderRawResponse = JsonSerializer.Serialize(r);
            job.ProviderMessageId = r.Message_ID;

            if (r.Status == "0")
            {
                job.Status = JobStatus.Sent;
                job.SentAt = DateTimeOffset.UtcNow;
                logger.LogInformation(
                    "[SMS] Sent OK to {ToNumber} | MsgId={MsgId}", job.ToNumber, r.Message_ID);
            }
            else
            {
                job.Status = JobStatus.Failed;
                job.FailureReason = r.Text;
                logger.LogWarning(
                    "[SMS] Failed for {ToNumber} | Reason={Reason}", job.ToNumber, r.Text);
            }
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.FailureReason = ex.Message;
            logger.LogError(ex, "[SMS] Exception dispatching to {ToNumber}", job.ToNumber);
        }
    }
}
