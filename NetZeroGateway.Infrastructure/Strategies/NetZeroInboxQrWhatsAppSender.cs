using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;

namespace NetZeroGateway.Infrastructure.Strategies;

/// <summary>
/// NetZero Inbox's QR-linked device API. Shares a host and a token with
/// <see cref="NetZeroInboxWhatsAppSender"/> and nothing else: a GET with query
/// parameters rather than a JSON POST, a <c>from</c> number the v1 API has none of, and
/// the recipient without the leading plus v1 requires.
/// </summary>
public class NetZeroInboxQrWhatsAppSender(
    INetZeroInboxQrClient client,
    ILogger<NetZeroInboxQrWhatsAppSender> logger
) : IChannelSender
{
    public ChannelType ChannelType => ChannelType.WP;
    public Provider Provider => Provider.NetZeroInboxQr;

    public async Task SendAsync(
        DispatchJob job, Dictionary<string, string> cfg, CancellationToken ct = default)
    {
        try
        {
            // No leading plus on this API, unlike v1 on the same host.
            var to = RecipientFormat.For(Provider.NetZeroInboxQr, job.ToNumber);

            // The linked device's own number. Without it the provider cannot tell which
            // WhatsApp session to send from, so refuse rather than send a blank one.
            if (!cfg.TryGetValue("from", out var from) || string.IsNullOrWhiteSpace(from))
            {
                job.Status = JobStatus.Failed;
                job.FailureReason =
                    "This channel has no sender number configured. Add the linked "
                    + "device's number in the channel's From Number field.";
                logger.LogWarning(
                    "[NZ_INBOX_QR] No 'from' configured for channel — send refused.");
                return;
            }

            // The QR API documents no media handling at all. As with v1, a clear
            // rejection beats guessing a parameter the vendor has not described.
            if (!string.IsNullOrWhiteSpace(job.MediaUrl))
            {
                job.Status = JobStatus.Failed;
                job.FailureReason =
                    "This WhatsApp channel does not support media. Send the message "
                    + "without a media URL, or use a WaTrend channel.";
                logger.LogWarning(
                    "[NZ_INBOX_QR] Media send rejected for {To} — provider has no "
                    + "documented media contract", job.ToNumber);
                return;
            }

            logger.LogInformation(
                "[NZ_INBOX_QR] Sending to {ToNumber} | RequestId={RequestId}",
                job.ToNumber, job.MessageRequestId);

            var r = await client.SendTextAsync(
                cfg["token"], RecipientFormat.ToCanonical(from), to, job.MessageContent,
                ProviderEndpoint.BaseUrlOf(cfg), ct);

            job.ProviderRawResponse = JsonSerializer.Serialize(r);

            if (r.IsSuccess)
            {
                job.Status = JobStatus.Sent;
                job.SentAt = DateTimeOffset.UtcNow;
                job.ProviderMessageId = r.ProviderMessageId;
                logger.LogInformation(
                    "[NZ_INBOX_QR] Sent OK to {ToNumber} | MsgId={MsgId}",
                    job.ToNumber, r.ProviderMessageId);
            }
            else
            {
                job.Status = JobStatus.Failed;
                job.FailureReason = r.Reason ?? "unknown_error";
                logger.LogWarning(
                    "[NZ_INBOX_QR] Failed for {ToNumber} | Reason={Reason}",
                    job.ToNumber, job.FailureReason);
            }
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.FailureReason = ex.Message;
            logger.LogError(ex, "[NZ_INBOX_QR] Exception dispatching to {ToNumber}", job.ToNumber);
        }
    }
}
