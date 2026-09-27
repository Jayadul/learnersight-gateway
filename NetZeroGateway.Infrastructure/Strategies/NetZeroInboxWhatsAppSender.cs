using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;

namespace NetZeroGateway.Infrastructure.Strategies;

public class NetZeroInboxWhatsAppSender(
    INetZeroInboxClient client,
    ILogger<NetZeroInboxWhatsAppSender> logger
) : IChannelSender
{
    public ChannelType ChannelType => ChannelType.WP;
    public Provider Provider => Provider.NetZeroInbox;

    public async Task SendAsync(
        DispatchJob job, Dictionary<string, string> cfg, CancellationToken ct = default)
    {
        try
        {
            // WhatsApp dispatches one job per recipient, so ToNumber is a single number.
            var to = RecipientFormat.For(Provider.NetZeroInbox, job.ToNumber);

            // The vendor has not documented a media type value. Guessing a request shape
            // would fail somewhere inside their API with an opaque error; refusing here
            // names the channel and the reason.
            if (!string.IsNullOrWhiteSpace(job.MediaUrl))
            {
                job.Status = JobStatus.Failed;
                job.FailureReason =
                    "This WhatsApp channel does not support media. Send the message "
                    + "without a media URL, or use a WaTrend channel.";
                logger.LogWarning(
                    "[NZ_INBOX] Media send rejected for {To} — provider has no documented "
                    + "media contract", job.ToNumber);
                return;
            }

            logger.LogInformation(
                "[NZ_INBOX] Sending to {ToNumber} | RequestId={RequestId}",
                job.ToNumber, job.MessageRequestId);

            var r = await client.SendTextAsync(
                cfg["token"], to, job.MessageContent,
                ProviderEndpoint.BaseUrlOf(cfg), ct);

            job.ProviderRawResponse = r.RawBody ?? JsonSerializer.Serialize(r);

            if (r.IsSuccess)
            {
                job.Status = JobStatus.Sent;
                job.SentAt = DateTimeOffset.UtcNow;
                // A wamid. No status endpoint exists for it yet; storing it costs
                // nothing and saves a re-integration if one appears.
                job.ProviderMessageId = r.MessageId;
                logger.LogInformation(
                    "[NZ_INBOX] Sent OK to {ToNumber} | MsgId={MsgId}", job.ToNumber, r.MessageId);
            }
            else
            {
                job.Status = JobStatus.Failed;
                job.FailureReason = r.Error ?? r.Message ?? r.Status ?? "unknown_error";
                logger.LogWarning(
                    "[NZ_INBOX] Failed for {ToNumber} | Reason={Reason}",
                    job.ToNumber, job.FailureReason);
            }
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.FailureReason = ex.Message;
            logger.LogError(ex, "[NZ_INBOX] Exception dispatching to {ToNumber}", job.ToNumber);
        }
    }
}
