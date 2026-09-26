using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;
using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.Strategies;

/// <summary>
/// NetZero Inbox's QR-linked device API. Shares a host and a token with
/// <see cref="NetZeroInboxWhatsAppSender"/> and nothing else: a GET with query
/// parameters rather than a JSON POST, a <c>from</c> number the v1 API has none of, and
/// the recipient without the leading plus v1 requires.
/// <para>
/// Confirmed to accept image media (<c>messageType=image</c>) — see
/// <c>hotfix-batch-1.md</c> item 1. Every other media type is still undocumented by the
/// vendor, so it is refused rather than sent on a guessed <c>messageType</c>.
/// </para>
/// </summary>
public class NetZeroInboxQrWhatsAppSender(
    INetZeroInboxQrClient client,
    ILogger<NetZeroInboxQrWhatsAppSender> logger
) : IChannelSender
{
    // Extensions confirmed to work as messageType=image. Anything else — including no
    // extension at all — is refused rather than guessed; the vendor has documented only
    // this one media path.
    private static readonly string[] ImageExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"];

    // Registered once at HandlesMedia=false (the default): one sender handles both
    // text and image for this provider, and the factory's single-sender fallback
    // routes a media job here too rather than requiring a second registration.
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

            var fromCanonical = RecipientFormat.ToCanonical(from);
            var baseUrl = ProviderEndpoint.BaseUrlOf(cfg);

            NetZeroInboxQrResponse r;
            if (string.IsNullOrWhiteSpace(job.MediaUrl))
            {
                logger.LogInformation(
                    "[NZ_INBOX_QR] Sending text to {ToNumber} | RequestId={RequestId}",
                    job.ToNumber, job.MessageRequestId);

                r = await client.SendTextAsync(
                    cfg["token"], fromCanonical, to, job.MessageContent, baseUrl, ct);
            }
            else if (IsImage(job.MediaUrl))
            {
                logger.LogInformation(
                    "[NZ_INBOX_QR] Sending image to {ToNumber} | RequestId={RequestId}",
                    job.ToNumber, job.MessageRequestId);

                r = await client.SendImageAsync(
                    cfg["token"], fromCanonical, to, job.MediaUrl, job.MessageContent,
                    baseUrl, ct);
            }
            else
            {
                // Only image is confirmed against the vendor's API. Guessing a
                // messageType for anything else risks a silent misfire on their side.
                job.Status = JobStatus.Failed;
                job.FailureReason =
                    "This channel only supports image media (jpg, jpeg, png, gif, webp, "
                    + "bmp) — other file types are not yet confirmed with this provider. "
                    + "Send as an image, or use a WaTrend channel.";
                logger.LogWarning(
                    "[NZ_INBOX_QR] Non-image media rejected for {To} | Url={Url}",
                    job.ToNumber, job.MediaUrl);
                return;
            }

            job.ProviderRawResponse = r.RawBody ?? JsonSerializer.Serialize(r);

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

    private static bool IsImage(string mediaUrl)
    {
        // Strip query string/fragment before reading the extension — a signed URL's
        // query params would otherwise be read as part of it.
        var path = mediaUrl.Split('?', '#')[0];
        var ext = Path.GetExtension(path);
        return ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }
}
