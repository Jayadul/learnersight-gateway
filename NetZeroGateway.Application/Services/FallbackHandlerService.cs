using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public class FallbackHandlerService(
    IDispatchJobRepository jobRepo,
    IDispatchJobRecipientRepository recipientRepo,
    IMessageRequestRepository msgRepo,
    IFallbackPolicyRepository policyRepo,
    IChannelConfigRepository configRepo,
    IEncryptionService enc,
    IChannelSenderFactory senderFactory,
    ILogger<FallbackHandlerService> logger
) : IFallbackHandlerService
{
    public async Task HandleAsync(Guid messageRequestId)
    {
        var jobs = await jobRepo.GetByMessageRequestIdAsync(messageRequestId);

        var failedPrimary = jobs
            .Where(j => j.Status == JobStatus.Failed && !j.IsFallback)
            .ToList();

        if (failedPrimary.Count == 0) return;

        // The sender already asked for more than one delivery, so a fallback send would be
        // a third message they never requested and did not budget for. Skip every job on
        // the request, not just the failed one, and say so loudly — a silent no-op here
        // looks identical to a missing policy when someone reads the logs later.
        var msgRequest = await msgRepo.GetByIdAsync(messageRequestId);
        if (msgRequest?.IsMultiChannel == true)
        {
            logger.LogWarning(
                "[FALLBACK] Skipped — message explicitly tagged multiple channels. "
                + "RequestId={Id} FailedJobs={Count}",
                messageRequestId, failedPrimary.Count);
            return;
        }

        var policies = await policyRepo.GetActiveGlobalPoliciesAsync();

        foreach (var job in failedPrimary)
        {
            // A policy scoped to one channel only covers failures on that channel;
            // an unscoped policy still covers every channel of its type.
            var policy = policies.FirstOrDefault(p =>
                p.TriggerChannel == job.ChannelType
                && (p.TriggerChannelConfigId is null
                    || p.TriggerChannelConfigId == job.ChannelConfigId));
            if (policy is null) continue;

            // Enforced here as well as at save time: a policy created before the rule
            // existed, or edited directly in the database, must not quietly cross types.
            if (policy.TriggerChannel != policy.FallbackChannel && !policy.AllowCrossChannel)
            {
                logger.LogWarning(
                    "[FALLBACK] Policy '{Name}' skipped — it crosses {Trigger} to {Fallback} "
                    + "without cross-channel fallback enabled.",
                    policy.Name, policy.TriggerChannel, policy.FallbackChannel);
                continue;
            }

            // A named fallback channel must be used as named; only an unnamed one falls
            // through to the type's default.
            var fallbackConfig = policy.FallbackChannelConfigId is { } targetId
                ? await configRepo.GetByIdAsync(targetId)
                : await configRepo.GetDefaultByChannelTypeAsync(policy.FallbackChannel);

            // Retrying on the channel that just failed would be pointless at best.
            if (fallbackConfig is not null && fallbackConfig.Id == job.ChannelConfigId)
            {
                logger.LogWarning(
                    "[FALLBACK] Policy '{Name}' skipped — its fallback resolves to the same "
                    + "channel that just failed ({ConfigId}).",
                    policy.Name, job.ChannelConfigId);
                continue;
            }

            if (fallbackConfig is not null && !fallbackConfig.IsActive)
            {
                logger.LogWarning(
                    "[FALLBACK] Policy '{Name}' skipped — its fallback channel '{Label}' is inactive.",
                    policy.Name, fallbackConfig.Label);
                continue;
            }

            if (fallbackConfig is null)
            {
                logger.LogWarning(
                    "[FALLBACK] Policy '{Name}' triggered but no active config for {FallbackChannel}",
                    policy.Name, policy.FallbackChannel);
                continue;
            }

            logger.LogInformation(
                "[FALLBACK] Triggered: {Trigger}→{Fallback} | Number={Number} | OriginalJob={JobId}",
                job.ChannelType, policy.FallbackChannel, job.ToNumber, job.Id);

            var fallbackJob = new DispatchJob
            {
                MessageRequestId = job.MessageRequestId,
                ChannelType = policy.FallbackChannel,
                ChannelConfigId = fallbackConfig.Id,
                MessageContent = job.MessageContent,
                MediaUrl = job.MediaUrl,
                ToNumber = job.ToNumber,
                IsFallback = true,
                FallbackFromJobId = job.Id,
                Status = JobStatus.Pending
            };
            await jobRepo.AddAsync(fallbackJob);
            await jobRepo.SaveChangesAsync();

            var cfg = JsonSerializer.Deserialize<Dictionary<string, string>>(
                enc.Decrypt(fallbackConfig.ConfigJson)) ?? [];

            var sender = senderFactory.GetSender(
                policy.FallbackChannel, fallbackConfig.Provider,
                fallbackJob.MediaUrl is not null);

            // The pacing delay applies to every attempt on a channel, so a fallback
            // dispatch waits too — otherwise a burst of failures would bypass the very
            // rate limiting the delay exists to respect.
            if (fallbackConfig.DelaySeconds > 0)
            {
                logger.LogInformation(
                    "[FALLBACK] Pacing {Channel} send by {Delay}s",
                    policy.FallbackChannel, fallbackConfig.DelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(fallbackConfig.DelaySeconds));
            }

            await sender.SendAsync(fallbackJob, cfg);
            await jobRepo.SaveChangesAsync();
            await RecordRecipientsAsync(fallbackJob);

            logger.LogInformation(
                "[FALLBACK] Complete. FallbackJobId={FbJobId} Status={Status}",
                fallbackJob.Id, fallbackJob.Status);
        }
    }

    /// <summary>
    /// Records the fallback attempt's recipients. A fallback job carries the original
    /// job's recipient list, so the rows mirror the primary attempt's — the two are told
    /// apart by their job's IsFallback flag, not by anything stored here.
    /// Failures are logged and swallowed: the message has already gone out.
    /// </summary>
    private async Task RecordRecipientsAsync(DispatchJob fallbackJob)
    {
        try
        {
            var rows = fallbackJob.ToNumber
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(n => new DispatchJobRecipient
                {
                    DispatchJobId = fallbackJob.Id,
                    Recipient = n.Trim(),
                    ProviderReferenceId = fallbackJob.ProviderMessageId,
                    DeliveryStatus = fallbackJob.Status == JobStatus.Failed
                        ? DeliveryStatus.Failed
                        : DeliveryStatus.Pending
                })
                .ToList();

            await recipientRepo.AddRangeAsync(rows);
            await recipientRepo.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "[FALLBACK] Could not record recipient rows for job {JobId}", fallbackJob.Id);
        }
    }
}
