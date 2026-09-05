using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public class ChannelDispatchService(
    IMessageRequestRepository msgRepo,
    IDispatchJobRepository jobRepo,
    IDispatchJobRecipientRepository recipientRepo,
    IChannelConfigRepository configRepo,
    IEncryptionService enc,
    IChannelSenderFactory senderFactory,
    IFallbackHandlerService fallbackHandler,
    ILogger<ChannelDispatchService> logger
) : IChannelDispatchService
{
    public async Task DispatchAsync(
        Guid messageRequestId, string toUser, List<ParsedChannelJob> jobs)
    {
        var numbers = toUser
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(n => n.Trim())
            .ToList();

        logger.LogInformation(
            "[DISPATCH] Start. RequestId={Id} Numbers={Count} JobTypes={Types}",
            messageRequestId, numbers.Count,
            string.Join(",", jobs.Select(j => j.ChannelType)));

        foreach (var job in jobs)
        {
            // A labelled tag names a specific active channel; anything else uses the
            // type's default.
            var config = job.ChannelLabelSlug is { } slug
                ? await configRepo.GetActiveBySlugAsync(job.ChannelType, slug)
                : await configRepo.GetDefaultByChannelTypeAsync(job.ChannelType);

            if (config is null)
            {
                if (job.ChannelLabelSlug is { } missing)
                {
                    // Fail loudly rather than silently falling back to the default: a
                    // typo'd slug must never bill a channel the sender did not name.
                    logger.LogWarning(
                        "[DISPATCH] No active {Channel} channel with label '{Slug}' — skipping. "
                        + "The label must match an active channel's slug exactly.",
                        job.ChannelType, missing);
                }
                else
                {
                    logger.LogWarning(
                        "[DISPATCH] No default config for {Channel} — skipping", job.ChannelType);
                }
                continue;
            }

            var cfg = JsonSerializer.Deserialize<Dictionary<string, string>>(
                enc.Decrypt(config.ConfigJson)) ?? [];

            switch (job.ChannelType)
            {
                case ChannelType.SMS:
                    await DispatchSmsAsync(
                        messageRequestId, config.Id, config.Provider, job, cfg, numbers);
                    break;
                case ChannelType.Voice:
                    await DispatchVoiceAsync(
                        messageRequestId, config.Id, config.Provider, job, cfg, numbers);
                    break;
                default:
                    await DispatchPerNumberAsync(
                        messageRequestId, config.Id, config.Provider, job, cfg, numbers,
                        config.DelaySeconds);
                    break;
            }
        }

        await fallbackHandler.HandleAsync(messageRequestId);
        await UpdateOverallStatusAsync(messageRequestId);

        logger.LogInformation("[DISPATCH] Complete. RequestId={Id}", messageRequestId);
    }

    // ── SMS — one call, comma-joined numbers ──────────────────────────────
    private async Task DispatchSmsAsync(
        Guid requestId, Guid configId, Provider provider,
        ParsedChannelJob job, Dictionary<string, string> cfg,
        List<string> numbers)
    {
        var dispatchJob = await CreateJobAsync(
            requestId, configId, job, string.Join(",", numbers));

        // A sender id supplied per message overrides the channel's configured one. cfg is
        // deserialized per job in DispatchAsync, so this cannot leak into another job.
        if (!string.IsNullOrWhiteSpace(job.CallerId))
            cfg["callerID"] = job.CallerId;

        var sender = senderFactory.GetSender(ChannelType.SMS, provider);
        await sender.SendAsync(dispatchJob, cfg);
        await jobRepo.SaveChangesAsync();
        await RecordRecipientsAsync(dispatchJob, numbers);
    }

    // ── WhatsApp Text/Media — one job per number, all in parallel ─────────
    private async Task DispatchPerNumberAsync(
        Guid requestId, Guid configId, Provider provider,
        ParsedChannelJob job, Dictionary<string, string> cfg,
        List<string> numbers, int delaySeconds)
    {
        // WhatsApp text and media share ChannelType.WP — the MediaUrl picks the sender.
        var sender = senderFactory.GetSender(
            job.ChannelType, provider, job.MediaUrl is not null);
        foreach (var number in numbers)
        {
            var dispatchJob = await CreateJobAsync(requestId, configId, job, number);
            await PaceAsync(delaySeconds, job.ChannelType);
            await sender.SendAsync(dispatchJob, cfg);
            await jobRepo.SaveChangesAsync();
            await RecordRecipientsAsync(dispatchJob, [number]);
        }
    }

    /// <summary>
    /// Waits the channel's configured pacing delay before a send. Applied to every
    /// attempt on that channel, so a long recipient list is spaced out end to end.
    /// Does not affect the HTTP client's own retries — those are Polly's, configured by
    /// HttpClients:Retry:DelaySeconds.
    /// </summary>
    private async Task PaceAsync(int delaySeconds, ChannelType channel)
    {
        if (delaySeconds <= 0) return;
        // Information, not Debug: pacing visibly changes how long a dispatch takes, so
        // an operator wondering why a batch is slow needs to see it in the normal logs.
        logger.LogInformation(
            "[DISPATCH] Pacing {Channel} send by {Delay}s", channel, delaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
    }

    // ── Voice — one call, full array ──────────────────────────────────────
    private async Task DispatchVoiceAsync(
        Guid requestId, Guid configId, Provider provider,
        ParsedChannelJob job, Dictionary<string, string> cfg,
        List<string> numbers)
    {
        var dispatchJob = await CreateJobAsync(
            requestId, configId, job, string.Join(",", numbers));

        // A lot number supplied per message ([call::campaignId||lotNumber]) overrides the
        // channel's configured default. cfg is deserialized per job in DispatchAsync, so
        // this overlay cannot leak into another job's dispatch.
        if (!string.IsNullOrWhiteSpace(job.LotNumber))
            cfg["lotNumber"] = job.LotNumber;

        var sender = senderFactory.GetSender(ChannelType.Voice, provider);
        await sender.SendAsync(dispatchJob, cfg);
        await jobRepo.SaveChangesAsync();
        await RecordRecipientsAsync(dispatchJob, numbers);
    }

    /// <summary>
    /// Writes one row per recipient of a completed job.
    /// <para>
    /// SMS and Voice batch every number into a single job, so without these rows a
    /// partially delivered batch has nowhere to record which numbers actually arrived.
    /// The provider reference is whatever the sender captured: REVE returns one id for
    /// the whole batch, so every row here carries the same one and will resolve to the
    /// same status — a REVE limitation, not a gap in the table. Voice returns none.
    /// </para>
    /// <para>
    /// Failure is swallowed deliberately. These rows exist for reporting and for the
    /// status sync to write into later; the message has already been handed to the
    /// provider by this point, and losing the audit rows must not turn a delivered
    /// send into a failed one.
    /// </para>
    /// </summary>
    private async Task RecordRecipientsAsync(DispatchJob dispatchJob, List<string> numbers)
    {
        try
        {
            var rows = numbers.Select(number => new DispatchJobRecipient
            {
                DispatchJobId = dispatchJob.Id,
                Recipient = number,
                ProviderReferenceId = dispatchJob.ProviderMessageId,
                // Pending means "accepted, nothing synced back yet". A job that failed
                // outright never reached the recipient, so it starts Failed rather than
                // waiting on a status that will never arrive.
                DeliveryStatus = dispatchJob.Status == JobStatus.Failed
                    ? DeliveryStatus.Failed
                    : DeliveryStatus.Pending
            }).ToList();

            await recipientRepo.AddRangeAsync(rows);
            await recipientRepo.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "[DISPATCH] Could not record recipient rows for job {JobId}", dispatchJob.Id);
        }
    }

    private async Task<DispatchJob> CreateJobAsync(
        Guid requestId, Guid configId, ParsedChannelJob job, string toNumber)
    {
        var dispatchJob = new DispatchJob
        {
            MessageRequestId = requestId,
            ChannelType = job.ChannelType,
            ChannelConfigId = configId,
            MessageContent = job.Message,
            MediaUrl = job.MediaUrl,
            CampaignId = job.CampaignId,
            ToNumber = toNumber,
            Status = JobStatus.Pending
        };
        await jobRepo.AddAsync(dispatchJob);
        await jobRepo.SaveChangesAsync();
        return dispatchJob;
    }

    private async Task UpdateOverallStatusAsync(Guid requestId)
    {
        var request = await msgRepo.GetWithJobsAsync(requestId);
        if (request is null) return;

        var primaryJobs = request.Jobs.Where(j => !j.IsFallback).ToList();
        var sentCount = primaryJobs.Count(j => j.Status == JobStatus.Sent);
        var failedCount = primaryJobs.Count(j => j.Status == JobStatus.Failed);

        // Every primary attempt failed but a retry got through, so the message did arrive
        // — just not by the route the sender asked for. Reporting that as Failed hid a
        // delivered message; reporting it as Success hid that a channel is broken.
        var rescuedByFallback = sentCount == 0
            && request.Jobs.Any(j => j.IsFallback && j.Status == JobStatus.Sent);

        // No jobs at all means nothing could be dispatched — every channel tag failed to
        // resolve. Without this it scores as Success (failedCount == 0), so a typo'd
        // channel label would look like a delivered message.
        request.OverallStatus = primaryJobs.Count == 0 ? MessageStatus.Failed
                              : failedCount == 0 ? MessageStatus.Success
                              : rescuedByFallback ? MessageStatus.Fallback
                              : sentCount == 0 ? MessageStatus.Failed
                              : MessageStatus.PartialSuccess;
        msgRepo.Update(request);
        await msgRepo.SaveChangesAsync();

        logger.LogInformation(
            "[DISPATCH] Status updated. RequestId={Id} Sent={Sent} Failed={Failed} Overall={Status}",
            requestId, sentCount, failedCount, request.OverallStatus);
    }
}
