using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public class ManualSendService(
    IMessageRequestRepository msgRepo,
    IServiceScopeFactory scopeFactory,
    ILogger<ManualSendService> logger
) : IManualSendService
{
    public async Task<ManualSendResponse> SendAsync(ManualSendRequest request)
    {
        var numbers = request.ToNumbers
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(n => n.Trim()).ToList();

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

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
