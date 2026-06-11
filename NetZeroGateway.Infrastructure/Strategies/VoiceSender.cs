using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;

namespace NetZeroGateway.Infrastructure.Strategies;

public class VoiceSender(
    INetZeroVoiceClient client,
    ILogger<VoiceSender> logger
) : IChannelSender
{
    public ChannelType ChannelType => ChannelType.Voice;

    public async Task SendAsync(
        DispatchJob job, Dictionary<string, string> cfg, CancellationToken ct = default)
    {
        try
        {
            // Voice job ToNumber is comma-joined; split for the API array
            var numbers = job.ToNumber
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(n => n.Trim()).ToList();

            var campaignId = job.CampaignId ?? cfg.GetValueOrDefault("campaignId", "");

            logger.LogInformation(
                "[VOICE] Running campaign {Campaign} for {Count} numbers | RequestId={RequestId}",
                campaignId, numbers.Count, job.MessageRequestId);

            var r = await client.RunCampaignAsync(
                cfg["owner"], cfg["password"], cfg["authUserId"],
                campaignId, cfg.GetValueOrDefault("lotNumber", "1"),
                numbers, ct);

            job.ProviderRawResponse = JsonSerializer.Serialize(r);

            if (r.IsSuccess)
            {
                job.Status = JobStatus.Sent;
                job.SentAt = DateTimeOffset.UtcNow;
                job.CampaignId = r.TaskId ?? job.CampaignId;
                logger.LogInformation(
                    "[VOICE] Campaign queued OK. TaskId={TaskId}", r.TaskId);
            }
            else
            {
                job.Status = JobStatus.Failed;
                job.FailureReason = r.Message;
                logger.LogWarning("[VOICE] Campaign failed | Reason={Reason}", r.Message);
            }
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.FailureReason = ex.Message;
            logger.LogError(ex, "[VOICE] Exception dispatching campaign");
        }
    }
}
