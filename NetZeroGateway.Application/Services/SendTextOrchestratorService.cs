using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    IChannelConfigRepository configRepo,
    IEncryptionService enc,
    IServiceScopeFactory scopeFactory,
    ILogger<SendTextOrchestratorService> logger
) : ISendTextOrchestratorService
{
    public async Task<SendTextResponse> ProcessAsync(SendTextRequest request)
    {
        if (!await IsAuthorizedAsync(request))
        {
            return SendTextResponse.Rejected();
        }

        var jobs = parser.Parse(request.MessageContent);

        var msgRequest = new MessageRequest
        {
            CallerID = request.CallerID ?? string.Empty,
            ToUser = request.ToUser,
            RawMessageContent = request.MessageContent,
            ParsedChannels = jobs.Select(j => j.ChannelType.ToString()).Distinct().ToList(),
            // More than one parsed job means the sender asked for the extra delivery
            // themselves, so fallback must not add a third. This counts jobs, not distinct
            // types: [smswp::..] and two SMS tags are both redundancy the sender paid for.
            IsMultiChannel = jobs.Count > 1,
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

    /// <summary>
    /// Third-party callers must supply apikey/secretkey/callerID. Keys are validated
    /// against the active SMS channel config, so rotating them in the admin panel
    /// immediately applies here too.
    /// </summary>
    private async Task<bool> IsAuthorizedAsync(SendTextRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey) ||
            string.IsNullOrWhiteSpace(request.SecretKey) ||
            string.IsNullOrWhiteSpace(request.CallerID))
        {
            logger.LogWarning("[GATEWAY_AUTH] Rejected — apikey/secretkey/callerID missing");
            return false;
        }

        var config = await configRepo.GetDefaultByChannelTypeAsync(ChannelType.SMS);
        if (config is null)
        {
            logger.LogWarning("[GATEWAY_AUTH] Rejected — no active SMS config to validate against");
            return false;
        }

        var cfg = JsonSerializer.Deserialize<Dictionary<string, string>>(
            enc.Decrypt(config.ConfigJson)) ?? [];

        var apiKeyOk = cfg.TryGetValue("apikey", out var expectedKey)
                       && FixedTimeEquals(request.ApiKey, expectedKey);
        var secretOk = cfg.TryGetValue("secretkey", out var expectedSecret)
                       && FixedTimeEquals(request.SecretKey, expectedSecret);

        if (!apiKeyOk || !secretOk)
        {
            logger.LogWarning("[GATEWAY_AUTH] Rejected — invalid apikey/secretkey. ToUser={ToUser}",
                request.ToUser);
            return false;
        }
        return true;
    }

    private static bool FixedTimeEquals(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
}
