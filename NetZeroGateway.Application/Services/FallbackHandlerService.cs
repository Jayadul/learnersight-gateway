using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public class FallbackHandlerService(
    IDispatchJobRepository      jobRepo,
    IFallbackPolicyRepository   policyRepo,
    IChannelConfigRepository    configRepo,
    IEncryptionService          enc,
    IChannelSenderFactory       senderFactory,
    ILogger<FallbackHandlerService> logger
) : IFallbackHandlerService
{
    public async Task HandleAsync(Guid messageRequestId)
    {
        var jobs     = await jobRepo.GetByMessageRequestIdAsync(messageRequestId);
        var policies = await policyRepo.GetActiveGlobalPoliciesAsync();

        var failedPrimary = jobs
            .Where(j => j.Status == JobStatus.Failed && !j.IsFallback)
            .ToList();

        if (failedPrimary.Count == 0) return;

        foreach (var job in failedPrimary)
        {
            var policy = policies.FirstOrDefault(p => p.TriggerChannel == job.ChannelType);
            if (policy is null) continue;

            var fallbackConfig = await configRepo.GetActiveByChannelTypeAsync(policy.FallbackChannel);
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
                MessageRequestId  = job.MessageRequestId,
                ChannelType       = policy.FallbackChannel,
                ChannelConfigId   = fallbackConfig.Id,
                MessageContent    = job.MessageContent,
                MediaUrl          = job.MediaUrl,
                ToNumber          = job.ToNumber,
                IsFallback        = true,
                FallbackFromJobId = job.Id,
                Status            = JobStatus.Pending
            };
            await jobRepo.AddAsync(fallbackJob);
            await jobRepo.SaveChangesAsync();

            var cfg = JsonSerializer.Deserialize<Dictionary<string, string>>(
                enc.Decrypt(fallbackConfig.ConfigJson)) ?? [];

            var sender = senderFactory.GetSender(policy.FallbackChannel);
            await sender.SendAsync(fallbackJob, cfg);
            await jobRepo.SaveChangesAsync();

            logger.LogInformation(
                "[FALLBACK] Complete. FallbackJobId={FbJobId} Status={Status}",
                fallbackJob.Id, fallbackJob.Status);
        }
    }
}
