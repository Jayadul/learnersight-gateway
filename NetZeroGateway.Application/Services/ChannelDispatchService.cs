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
            var config = await configRepo.GetActiveByChannelTypeAsync(job.ChannelType);
            if (config is null)
            {
                logger.LogWarning(
                    "[DISPATCH] No active config for {Channel} — skipping", job.ChannelType);
                continue;
            }

            var cfg = JsonSerializer.Deserialize<Dictionary<string, string>>(
                enc.Decrypt(config.ConfigJson)) ?? [];

            switch (job.ChannelType)
            {
                case ChannelType.SMS:
                    await DispatchSmsAsync(messageRequestId, config.Id, job, cfg, numbers);
                    break;
                case ChannelType.Voice:
                    await DispatchVoiceAsync(messageRequestId, config.Id, job, cfg, numbers);
                    break;
                default:
                    await DispatchPerNumberAsync(messageRequestId, config.Id, job, cfg, numbers);
                    break;
            }
        }

        await fallbackHandler.HandleAsync(messageRequestId);
        await UpdateOverallStatusAsync(messageRequestId);

        logger.LogInformation("[DISPATCH] Complete. RequestId={Id}", messageRequestId);
    }

    // ── SMS — one call, comma-joined numbers ──────────────────────────────
    private async Task DispatchSmsAsync(
        Guid requestId, Guid configId,
        ParsedChannelJob job, Dictionary<string, string> cfg,
        List<string> numbers)
    {
        var dispatchJob = await CreateJobAsync(
            requestId, configId, job, string.Join(",", numbers));

        var sender = senderFactory.GetSender(ChannelType.SMS);
        await sender.SendAsync(dispatchJob, cfg);
        await jobRepo.SaveChangesAsync();
    }

    // ── WhatsApp Text/Media — one job per number, all in parallel ─────────
    private async Task DispatchPerNumberAsync(
        Guid requestId, Guid configId,
        ParsedChannelJob job, Dictionary<string, string> cfg,
        List<string> numbers)
    {
        var sender = senderFactory.GetSender(job.ChannelType);
        foreach (var number in numbers)
        {
            var dispatchJob = await CreateJobAsync(requestId, configId, job, number);
            await sender.SendAsync(dispatchJob, cfg);
            await jobRepo.SaveChangesAsync();
        }
    }

    // ── Voice — one call, full array ──────────────────────────────────────
    private async Task DispatchVoiceAsync(
        Guid requestId, Guid configId,
        ParsedChannelJob job, Dictionary<string, string> cfg,
        List<string> numbers)
    {
        var dispatchJob = await CreateJobAsync(
            requestId, configId, job, string.Join(",", numbers));

        var sender = senderFactory.GetSender(ChannelType.Voice);
        await sender.SendAsync(dispatchJob, cfg);
        await jobRepo.SaveChangesAsync();
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

        request.OverallStatus = failedCount == 0 ? MessageStatus.Success
                              : sentCount == 0 ? MessageStatus.Failed
                              : MessageStatus.PartialSuccess;
        msgRepo.Update(request);
        await msgRepo.SaveChangesAsync();

        logger.LogInformation(
            "[DISPATCH] Status updated. RequestId={Id} Sent={Sent} Failed={Failed} Overall={Status}",
            requestId, sentCount, failedCount, request.OverallStatus);
    }
}
