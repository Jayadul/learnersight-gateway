using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;

namespace NetZeroGateway.Infrastructure.Strategies;

public class GenNetSmsSender(
    IGenNetClient client,
    ILogger<GenNetSmsSender> logger
) : IChannelSender
{
    public ChannelType ChannelType => ChannelType.SMS;
    public Provider Provider => Provider.GenNet;

    public async Task SendAsync(
        DispatchJob job, Dictionary<string, string> cfg, CancellationToken ct = default)
    {
        try
        {
            // SMS jobs batch every recipient into one comma-joined ToNumber, and GenNet
            // is a genuinely bulk endpoint, so the whole job is one call.
            var recipients = job.ToNumber
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(n => RecipientFormat.For(Provider.GenNet, n.Trim()))
                .ToList();

            // Exactly 20 characters — GenNet rejects any other length with
            // "Invalid CSMS ID" over HTTP 200, so a GUID fails every send.
            var batchId = CsmsId.New();

            logger.LogInformation(
                "[GENNET] Sending to {Count} recipients | RequestId={RequestId}",
                recipients.Count, job.MessageRequestId);

            var r = await client.SendBulkAsync(
                cfg["api_token"], cfg["sid"],
                recipients, job.MessageContent, batchId,
                ProviderEndpoint.BaseUrlOf(cfg), ct);

            job.ProviderRawResponse = JsonSerializer.Serialize(r);

            if (r.IsSuccess)
            {
                job.Status = JobStatus.Sent;
                job.SentAt = DateTimeOffset.UtcNow;
                // The batch id is ours, not the vendor's, but it is the only handle that
                // identifies this job in GenNet's system. Per-recipient reference ids
                // live on the recipient rows.
                job.ProviderMessageId = batchId;
                logger.LogInformation(
                    "[GENNET] Sent OK. Batch={Batch} Recipients={Count}",
                    batchId, r.SmsInfo?.Count ?? recipients.Count);
            }
            else
            {
                job.Status = JobStatus.Failed;
                job.FailureReason = r.Reason ?? "unknown_error";
                logger.LogWarning("[GENNET] Failed | Reason={Reason}", job.FailureReason);
            }
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.FailureReason = ex.Message;
            logger.LogError(ex, "[GENNET] Exception dispatching job {JobId}", job.Id);
        }
    }
}
