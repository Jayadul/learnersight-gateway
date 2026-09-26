using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

/// <summary>
/// Retries individual recipients that failed.
/// <para>
/// Recipient-level rather than job-level: SMS and Voice batch many numbers into one
/// job, so resending the job would send again to everyone it already reached.
/// </para>
/// <para>
/// Hotfix batch 4, task 2a reverses the earlier "every attempt is a new linked row"
/// design: three retries of one recipient used to leave three separate rows in Message
/// Logs, which read as three different sends rather than one recipient retried three
/// times. The recipient row is now updated in place on every attempt — same id, latest
/// job, latest status — while each attempt's own outcome is kept in
/// <see cref="Interfaces.IResendAttemptRepository"/> so "what happened on attempt 2 vs 3"
/// stays recoverable in the row's history even though the main table only ever shows one
/// current row per recipient.
/// </para>
/// </summary>
public class ResendService(
    IDispatchJobRepository jobRepo,
    IDispatchJobRecipientRepository recipientRepo,
    IResendAttemptRepository attemptRepo,
    IChannelConfigRepository configRepo,
    IEncryptionService enc,
    IChannelSenderFactory senderFactory,
    ILogger<ResendService> logger
) : IResendService
{
    /// <summary>
    /// Bounded so one click cannot spend an unbounded amount of the client's provider
    /// credit. A larger recovery is several deliberate batches.
    /// </summary>
    private const int MaxPerRequest = 200;

    public async Task<ResendResultDto> ResendAsync(ResendRequest request)
    {
        if (request.RecipientIds.Count == 0)
            throw new InvalidOperationException("Select at least one recipient to resend.");

        if (request.RecipientIds.Count > MaxPerRequest)
        {
            throw new InvalidOperationException(
                $"Up to {MaxPerRequest} recipients can be resent at once; "
                + $"{request.RecipientIds.Count} were selected. Split the selection.");
        }

        var result = new ResendResultDto { Requested = request.RecipientIds.Count };

        // Distinct: the same row selected twice must not bill two sends.
        foreach (var recipientId in request.RecipientIds.Distinct())
        {
            var outcome = await ResendOneAsync(recipientId, request.ChannelConfigId);
            result.Outcomes.Add(outcome);

            if (outcome.Sent) result.Resent++;
            else if (outcome.Attempted) result.Failed++;
            else result.Skipped++;
        }

        logger.LogInformation(
            "[RESEND] requested={Requested} resent={Resent} failed={Failed} skipped={Skipped}",
            result.Requested, result.Resent, result.Failed, result.Skipped);

        return result;
    }

    private async Task<ResendRecipientOutcome> ResendOneAsync(
        Guid recipientId, Guid? overrideChannelId)
    {
        var original = await recipientRepo.GetByIdAsync(recipientId);
        if (original is null)
        {
            return new ResendRecipientOutcome
            {
                RecipientId = recipientId,
                Reason = "This recipient no longer exists."
            };
        }

        var outcome = new ResendRecipientOutcome
        {
            RecipientId = recipientId,
            Recipient = original.Recipient
        };

        var originalJob = await jobRepo.GetByIdAsync(original.DispatchJobId);
        if (originalJob is null)
        {
            outcome.Reason = "The original dispatch job no longer exists.";
            return outcome;
        }

        var channel = await ResolveChannelAsync(originalJob, overrideChannelId, outcome);
        if (channel is null) return outcome;

        var cfg = JsonSerializer.Deserialize<Dictionary<string, string>>(
            enc.Decrypt(channel.ConfigJson)) ?? [];

        // A fresh job carrying exactly this one recipient, whatever the original's
        // batching was. IsFallback stays false: this is a new deliberate attempt, not
        // an automatic retry, and it goes through fallback again on its own merits.
        var job = new DispatchJob
        {
            MessageRequestId = originalJob.MessageRequestId,
            ChannelType = channel.ChannelType,
            ChannelConfigId = channel.Id,
            MessageContent = originalJob.MessageContent,
            MediaUrl = originalJob.MediaUrl,
            CampaignId = originalJob.CampaignId,
            ToNumber = original.Recipient,
            Status = JobStatus.Pending
        };
        await jobRepo.AddAsync(job);
        await jobRepo.SaveChangesAsync();

        outcome.Attempted = true;
        var sender = senderFactory.GetSender(
            channel.ChannelType, channel.Provider, job.MediaUrl is not null);
        await sender.SendAsync(job, cfg);
        await jobRepo.SaveChangesAsync();

        // Updated in place, not a new row — see the class remark. The row now reflects
        // this latest attempt; the sync fields reset because they describe the new job,
        // not the one that just got replaced.
        original.DispatchJobId = job.Id;
        original.ProviderReferenceId = job.ProviderMessageId;
        original.DeliveryStatus = DeliveryStatusRules.InitialFor(channel.ChannelType, job.Status);
        original.LastCheckedAt = null;
        original.RawStatusResponse = null;
        recipientRepo.Update(original);
        await recipientRepo.SaveChangesAsync();

        await attemptRepo.AddAsync(new ResendAttempt
        {
            DispatchJobRecipientId = original.Id,
            DispatchJobId = job.Id,
            DeliveryStatus = original.DeliveryStatus,
            ProviderReferenceId = job.ProviderMessageId,
            RawResponse = job.ProviderRawResponse,
        });
        await attemptRepo.SaveChangesAsync();

        outcome.Sent = job.Status == JobStatus.Sent;
        outcome.Reason = job.FailureReason;

        logger.LogInformation(
            "[RESEND] {Recipient} via {Channel} -> {Status}",
            original.Recipient, channel.Label, job.Status);

        return outcome;
    }

    /// <summary>
    /// The channel to retry on: the override when given, otherwise the original.
    /// Crossing channel types is refused — the same type-safety rule fallback uses.
    /// An SMS-only contact has no guaranteed WhatsApp number, and the recipient format
    /// assumptions differ per channel, so a cross-type resend is a separate feature
    /// rather than a looser version of this one.
    /// </summary>
    private async Task<ChannelConfig?> ResolveChannelAsync(
        DispatchJob originalJob, Guid? overrideChannelId, ResendRecipientOutcome outcome)
    {
        if (overrideChannelId is not { } channelId)
        {
            var original = await configRepo.GetByIdAsync(originalJob.ChannelConfigId);
            if (original is null)
            {
                outcome.Reason = "The channel this was sent on no longer exists. "
                               + "Pick another channel to resend through.";
                return null;
            }

            if (!original.IsActive)
            {
                outcome.Reason = $"'{original.Label}' is no longer active. "
                               + "Pick another channel to resend through.";
                return null;
            }

            return original;
        }

        var target = await configRepo.GetByIdAsync(channelId)
            ?? throw new NotFoundException($"ChannelConfig {channelId} not found.");

        if (target.ChannelType != originalJob.ChannelType)
        {
            throw new InvalidOperationException(
                $"A {originalJob.ChannelType} message cannot be resent on a "
                + $"{target.ChannelType} channel. Pick a channel of the same type.");
        }

        if (!target.IsActive)
        {
            throw new InvalidOperationException(
                $"'{target.Label}' is not active, so nothing can be sent through it.");
        }

        return target;
    }
}
