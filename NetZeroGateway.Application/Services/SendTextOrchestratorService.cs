using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public class SendTextOrchestratorService(
    ITemplateParserService parser,
    IMessageRequestRepository msgRepo,
    IServiceScopeFactory scopeFactory,
    ILogger<SendTextOrchestratorService> logger
) : ISendTextOrchestratorService
{
    public async Task<SendTextResponse> ProcessAsync(SendTextRequest request)
    {
        var jobs = parser.Parse(request.MessageContent);

        var msgRequest = new MessageRequest
        {
            CallerID = request.CallerID ?? string.Empty,
            ToUser = request.ToUser,
            RawMessageContent = request.MessageContent,
            ParsedChannels = jobs.Select(j => j.ChannelType.ToString()).Distinct().ToList(),
            OverallStatus = MessageStatus.Pending,
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = MessageSource.Api
        };
        await msgRepo.AddAsync(msgRequest);
        await msgRepo.SaveChangesAsync();

        logger.LogInformation(
            "[ORCHESTRATOR] Queued. RequestId={Id} Channels={Channels} ToUser={ToUser}",
            msgRequest.Id,
            string.Join(",", jobs.Select(j => j.ChannelType)),
            request.ToUser);

        var capturedId = msgRequest.Id;
        var capturedToUser = request.ToUser;
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
                    "[ORCHESTRATOR] Background dispatch failed. RequestId={Id}", capturedId);
            }
        });

        return SendTextResponse.Accepted(msgRequest.Id);
    }
}
