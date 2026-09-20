using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public class ManualSendService(
    IMessageRequestRepository msgRepo,
    IServiceScopeFactory scopeFactory,
    IChannelConfigRepository configRepo,
    IDispatchJobRepository jobRepo,
    IDispatchJobRecipientRepository recipientRepo,
    IEncryptionService enc,
    IWhatsAppTemplateSender templateSender,
    ILogger<ManualSendService> logger
) : IManualSendService
{
    public async Task<ManualSendResponse> SendAsync(ManualSendRequest request)
    {
        var numbers = request.ToNumbers
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .ToList();

        // Rejected here rather than accepted and failed later. A request with no
        // recipients used to return 202 and then resolve to Failed, which reads in the
        // log as a delivery problem rather than a malformed request — and left a row
        // that never had anywhere to go. A comma-only string lands here too.
        if (numbers.Count == 0)
        {
            throw new InvalidOperationException(
                "Enter at least one recipient number.");
        }

        if (string.IsNullOrWhiteSpace(request.Message)
            && string.IsNullOrWhiteSpace(request.CampaignId))
        {
            // Voice carries a campaign instead of body text, so either satisfies this.
            throw new InvalidOperationException(
                "Enter a message to send.");
        }

        var jobs = new List<ParsedChannelJob>
        {
            new()
            {
                ChannelType      = request.ChannelType,
                Message          = request.Message,
                MediaUrl         = Blank(request.MediaUrl),
                // Blank means "not overridden", so an empty box never clears a configured
                // value — dispatch only overlays what is actually set.
                ChannelLabelSlug = Blank(request.ChannelLabelSlug),
                CallerId         = Blank(request.CallerId),
                CampaignId       = Blank(request.CampaignId),
                LotNumber        = Blank(request.LotNumber)
            }
        };

        var msgRequest = new MessageRequest
        {
            CallerID = request.CallerId ?? "",
            ToUser = request.ToNumbers,
            RawMessageContent = request.Message,
            ParsedChannels = [request.ChannelType.ToString()],
            // The admin panel sends on exactly one channel, so fallback stays available.
            IsMultiChannel = false,
            OverallStatus = MessageStatus.Pending,
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = MessageSource.AdminPanel
        };
        await msgRepo.AddAsync(msgRequest);
        await msgRepo.SaveChangesAsync();

        logger.LogInformation(
            "[MANUAL_SEND] Queued. RequestId={Id} Channel={Channel} Numbers={Count}",
            msgRequest.Id, request.ChannelType, numbers.Count);

        var capturedId = msgRequest.Id;
        var capturedToUser = request.ToNumbers;
        var capturedJobs = jobs;

        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var dispatchService = scope.ServiceProvider
                .GetRequiredService<IChannelDispatchService>();
            try
            {
                await dispatchService.DispatchAsync(capturedId, capturedToUser, capturedJobs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[MANUAL_SEND] Background dispatch failed. RequestId={Id}", capturedId);
            }
        });

        return new ManualSendResponse
        {
            MessageRequestId = msgRequest.Id,
            Status = "queued",
            TotalNumbers = numbers.Count
        };
    }

    public async Task<ManualSendResponse> SendTemplateAsync(SendTemplateRequest request)
    {
        var toNumber = (request.ToNumber ?? "").Trim();
        if (toNumber.Length == 0)
            throw new InvalidOperationException("Enter a recipient number.");
        if (string.IsNullOrWhiteSpace(request.TemplateName))
            throw new InvalidOperationException("Enter a template name.");

        var channel = await configRepo.GetActiveBySlugAsync(ChannelType.WP, request.ChannelLabelSlug)
            ?? throw new NotFoundException(
                $"No active WhatsApp channel with label '{request.ChannelLabelSlug}'.");

        if (channel.Provider != Provider.NetZeroInbox)
            throw new InvalidOperationException(
                "WhatsApp templates are only supported on NetZero Inbox (v1) channels.");

        var cfg = JsonSerializer.Deserialize<Dictionary<string, string>>(
            enc.Decrypt(channel.ConfigJson)) ?? [];
        if (!cfg.TryGetValue("templateToken", out var templateToken)
            || string.IsNullOrWhiteSpace(templateToken))
        {
            throw new InvalidOperationException(
                $"'{channel.Label}' has no Template API Token configured. "
                + "Add one on this channel before sending a template.");
        }

        // The vendor's template API wants E.164, unlike the canonical stored form every
        // other call on this channel uses — RecipientFormat.ToPlus is the same conversion
        // NetZero Inbox's regular text send already applies.
        var e164 = RecipientFormat.ToPlus(toNumber);

        var msgRequest = new MessageRequest
        {
            CallerID = "",
            ToUser = toNumber,
            RawMessageContent = $"[template:{request.TemplateName}]",
            ParsedChannels = [ChannelType.WP.ToString()],
            IsMultiChannel = false,
            OverallStatus = MessageStatus.Pending,
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = MessageSource.AdminPanel
        };
        await msgRepo.AddAsync(msgRequest);
        await msgRepo.SaveChangesAsync();

        var job = new DispatchJob
        {
            MessageRequestId = msgRequest.Id,
            ChannelType = ChannelType.WP,
            ChannelConfigId = channel.Id,
            MessageContent = $"Template '{request.TemplateName}' with {request.ExampleArr.Count} variable(s)",
            MediaUrl = Blank(request.MediaUrl),
            ToNumber = toNumber,
            Status = JobStatus.Pending
        };
        await jobRepo.AddAsync(job);
        await jobRepo.SaveChangesAsync();

        var result = await templateSender.SendAsync(
            templateToken, e164, request.TemplateName, request.ExampleArr,
            Blank(request.MediaUrl), ProviderEndpoint.BaseUrlOf(cfg));

        job.ProviderRawResponse = result.RawResponse;
        if (result.IsSuccess)
        {
            job.Status = JobStatus.Sent;
            job.SentAt = DateTimeOffset.UtcNow;
            // No status-check endpoint exists for this yet, but stored the same way as
            // other providers' message ids for consistency should one appear later.
            job.ProviderMessageId = result.MessageId;
        }
        else
        {
            job.Status = JobStatus.Failed;
            job.FailureReason = result.Error;
        }
        jobRepo.Update(job);
        await jobRepo.SaveChangesAsync();

        await recipientRepo.AddAsync(new DispatchJobRecipient
        {
            DispatchJobId = job.Id,
            Recipient = toNumber,
            ProviderReferenceId = result.MessageId,
            DeliveryStatus = job.Status == JobStatus.Failed ? DeliveryStatus.Failed : DeliveryStatus.Pending
        });
        await recipientRepo.SaveChangesAsync();

        msgRequest.OverallStatus = job.Status == JobStatus.Sent ? MessageStatus.Success : MessageStatus.Failed;
        msgRepo.Update(msgRequest);
        await msgRepo.SaveChangesAsync();

        logger.LogInformation(
            "[MANUAL_SEND_TEMPLATE] {To} via {Channel} template={Template} -> {Status}",
            toNumber, channel.Label, request.TemplateName, job.Status);

        return new ManualSendResponse
        {
            MessageRequestId = msgRequest.Id,
            Status = job.Status == JobStatus.Sent ? "sent" : "failed",
            TotalNumbers = 1
        };
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
