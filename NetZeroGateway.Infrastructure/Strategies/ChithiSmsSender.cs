using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;

namespace NetZeroGateway.Infrastructure.Strategies;

public class ChithiSmsSender(
    IChithiClient client,
    ILogger<ChithiSmsSender> logger
) : IChannelSender
{
    public ChannelType ChannelType => ChannelType.SMS;
    public Provider Provider => Provider.Chithi;

    public async Task SendAsync(
        DispatchJob job, Dictionary<string, string> cfg, CancellationToken ct = default)
    {
        try
        {
            // Chithi takes one recipient per call, but SMS jobs are batched, so a job
            // with several numbers becomes several calls. The job is Sent when every
            // call succeeded and Failed when any did not — a job-level status cannot say
            // "three of five", which is what the recipient rows are for.
            var recipients = job.ToNumber
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(n => RecipientFormat.For(Provider.Chithi, n.Trim()))
                .ToList();

            logger.LogInformation(
                "[CHITHI] Sending to {Count} recipients | RequestId={RequestId}",
                recipients.Count, job.MessageRequestId);

            var responses = new List<ChithiCallResult>();
            var baseUrl = ProviderEndpoint.BaseUrlOf(cfg);

            foreach (var msisdn in recipients)
            {
                var csmsId = CsmsId.New();
                var r = await client.SendAsync(
                    cfg["api_token"], cfg["sid"], msisdn, job.MessageContent, csmsId,
                    baseUrl, ct);

                responses.Add(new ChithiCallResult(msisdn, csmsId, r.IsSuccess,
                    r.Reason, r.ReferenceId));
            }

            // The whole set is stored, not just the last call, so a partial failure is
            // still readable in the delivery log.
            job.ProviderRawResponse = JsonSerializer.Serialize(responses);

            var failed = responses.Where(x => !x.Success).ToList();
            if (failed.Count == 0)
            {
                job.Status = JobStatus.Sent;
                job.SentAt = DateTimeOffset.UtcNow;
                // One reference id per recipient, so there is no single job-level id.
                // The first stands in for the job; the rest live on the recipient rows.
                job.ProviderMessageId = responses.FirstOrDefault()?.ReferenceId;

                // One id per recipient here too, and unlike GenNet each came from its
                // own call, so the mapping is exact.
                job.RecipientReferences = responses
                    .Where(x => x.ReferenceId is not null)
                    .GroupBy(x => RecipientFormat.ToCanonical(x.Msisdn))
                    .ToDictionary(g => g.Key, g => g.First().ReferenceId!);

                logger.LogInformation("[CHITHI] Sent OK to {Count} recipients", responses.Count);
            }
            else
            {
                job.Status = JobStatus.Failed;
                job.FailureReason = failed.Count == responses.Count
                    ? failed[0].Reason ?? "unknown_error"
                    : $"{failed.Count} of {responses.Count} recipients failed: "
                      + (failed[0].Reason ?? "unknown_error");
                logger.LogWarning("[CHITHI] Failed | Reason={Reason}", job.FailureReason);
            }
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.FailureReason = ex.Message;
            logger.LogError(ex, "[CHITHI] Exception dispatching job {JobId}", job.Id);
        }
    }

    /// <summary>One recipient's outcome within a batched Chithi job.</summary>
    private sealed record ChithiCallResult(
        string Msisdn, string CsmsId, bool Success, string? Reason, string? ReferenceId);
}
