using System.Security.Cryptography;
using System.Text;
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
    IChannelTemplateRepository templateRepo,
    IGatewayCredentialRepository credentialRepo,
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

        var parsed = parser.Parse(request.MessageContent);
        if (!parsed.IsValid)
        {
            // Rejecting beats accepting: an unknown tag produces no job, so accepting the
            // request would report success for a message that never went anywhere. The
            // reason travels back to the caller and into api_logs with the response body.
            logger.LogWarning(
                "[GATEWAY] Rejected — {Reason} ToUser={ToUser}", parsed.Error, request.ToUser);
            return SendTextResponse.Rejected(parsed.Error);
        }

        var jobs = parsed.Jobs;

        if (await UnresolvableLabelAsync(jobs) is { } labelError)
        {
            logger.LogWarning(
                "[GATEWAY] Rejected — {Reason} ToUser={ToUser}", labelError, request.ToUser);
            return SendTextResponse.Rejected(labelError);
        }

        if (await InvalidTemplateAsync(jobs) is { } templateError)
        {
            logger.LogWarning(
                "[GATEWAY] Rejected — {Reason} ToUser={ToUser}", templateError, request.ToUser);
            return SendTextResponse.Rejected(templateError);
        }

        var msgRequest = new MessageRequest
        {
            CallerID = request.CallerID ?? string.Empty,
            ToUser = request.ToUser,
            RawMessageContent = request.MessageContent,
            ParsedChannels = jobs.Select(j => j.ChannelType.ToString()).Distinct().ToList(),
            // More than one parsed job means the sender asked for the extra delivery
            // themselves, so fallback must not add a third. This counts jobs, not distinct
            // types: a combined tag and two SMS tags are both redundancy the sender paid for.
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
    /// Checks every label names an active channel, and returns the reason for the first
    /// that does not. Dispatch happens after the caller has been told 202, so a label
    /// that cannot resolve has to be caught here — otherwise the only trace of a typo'd
    /// slug is a warning in Seq, long after the sender was told the message was accepted.
    /// </summary>
    private async Task<string?> UnresolvableLabelAsync(List<ParsedChannelJob> jobs)
    {
        foreach (var job in jobs)
        {
            // Only tagged sends carry a label; an untagged message uses the default SMS
            // channel and has nothing to resolve.
            if (job.ChannelLabelSlug is not { } slug) continue;

            if (await configRepo.GetActiveBySlugAsync(job.ChannelType, slug) is null)
                return $"No active {job.ChannelType} channel is labelled '{slug}'. "
                     + "The label must match an active channel's slug in the admin panel exactly.";
        }

        return null;
    }

    /// <summary>
    /// Hotfix batch 9, Task 10: rejects a <c>template</c> tag at accept time, same as an
    /// unresolvable label — a template name that does not exist, or the wrong variable
    /// count, cannot be allowed through only to fail invisibly against Meta's API during
    /// background dispatch. Checked against the channel's own registry (Task 1).
    /// </summary>
    private async Task<string?> InvalidTemplateAsync(List<ParsedChannelJob> jobs)
    {
        foreach (var job in jobs)
        {
            if (job.TemplateName is not { } name) continue;

            if (string.IsNullOrWhiteSpace(name))
                return "A template tag must name the template: "
                     + "[template:label::Template_Name||Variable1||Variable2].";

            // The label already resolved to an active WP channel (UnresolvableLabelAsync
            // ran first) — every template tag carries a label, so this is never null.
            var channel = await configRepo.GetActiveBySlugAsync(ChannelType.WP, job.ChannelLabelSlug!);
            if (channel is null) continue;

            if (channel.Provider != Domain.Enums.Provider.NetZeroInboxTemplate)
                return $"'{channel.Label}' is not a WhatsApp Template channel. "
                     + "Point the template tag at a channel set up for templates.";

            var template = await templateRepo.GetByChannelAndNameAsync(channel.Id, name);
            if (template is null)
                return $"No template named '{name}' is registered on channel '{channel.Label}'. "
                     + "Register it there first, under Channels.";

            var expected = template.VariableLabels.Count;
            var got = job.TemplateVariables?.Count ?? 0;
            if (expected != got)
                return $"Template '{name}' expects {expected} variable(s) but the tag provided {got}.";
        }

        return null;
    }

    /// <summary>
    /// Third-party callers must supply apikey/secretkey/callerID. The keys are validated
    /// against the gateway's own credentials, so rotating them in the admin panel applies
    /// here immediately.
    /// <para>
    /// Until Release 2.1 these were read off the default SMS channel config. That made
    /// gateway access a side effect of whichever SMS provider happened to be the default
    /// — see docs/adr/0007-decouple-gateway-auth-from-channel-config.md.
    /// </para>
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

        var credential = await credentialRepo.GetCurrentAsync();
        if (credential is null)
        {
            logger.LogError(
                "[GATEWAY_AUTH] Rejected — no gateway credentials exist. Seed them via "
                + "Seed:GatewayCredentials, or set them under Settings in the admin panel.");
            return false;
        }

        string expectedKey, expectedSecret;
        try
        {
            expectedKey = enc.Decrypt(credential.ApiKeyEncrypted);
            expectedSecret = enc.Decrypt(credential.SecretKeyEncrypted);
        }
        catch (Exception ex)
        {
            // Almost always a changed encryption key: the stored ciphertext predates it.
            // Rejecting is right, but the cause is not the caller's, so say so.
            logger.LogError(ex,
                "[GATEWAY_AUTH] Rejected — stored gateway credentials could not be decrypted. "
                + "Re-enter them under Settings in the admin panel.");
            return false;
        }

        var apiKeyOk = FixedTimeEquals(request.ApiKey, expectedKey);
        var secretOk = FixedTimeEquals(request.SecretKey, expectedSecret);

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
