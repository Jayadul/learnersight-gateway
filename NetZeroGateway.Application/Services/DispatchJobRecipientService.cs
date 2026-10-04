using System.Text.Json;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public class DispatchJobRecipientService(
    IDispatchJobRecipientRepository repo,
    IResendAttemptRepository attemptRepo
) : IDispatchJobRecipientService
{
    public async Task<PagedResult<DispatchJobRecipientDto>> GetPagedAsync(
        int page, int pageSize,
        DateTimeOffset? dateFrom, DateTimeOffset? dateTo,
        ChannelType? channelType, Guid? channelConfigId,
        DeliveryStatus? deliveryStatus, string? recipient,
        string? failureReason = null)
    {
        var (items, total) = await repo.GetPagedAsync(
            page, pageSize, dateFrom, dateTo, channelType, channelConfigId,
            deliveryStatus, recipient, failureReason);

        return new PagedResult<DispatchJobRecipientDto>
        {
            Items = items.Select(ToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static DispatchJobRecipientDto ToDto(DispatchJobRecipient r) => new()
    {
        Id = r.Id,
        DispatchJobId = r.DispatchJobId,
        Recipient = r.Recipient,
        ProviderReferenceId = r.ProviderReferenceId,
        DeliveryStatus = r.DeliveryStatus,
        LastCheckedAt = r.LastCheckedAt,
        CreatedAt = r.CreatedAt,
        CallStartTime = r.CallStartTime,
        CallConnectTime = r.CallConnectTime,
        CallEndTime = r.CallEndTime,
        ChannelType = r.DispatchJob!.ChannelType,
        ChannelLabel = r.DispatchJob.ChannelConfig?.Label,
        MessageContent = r.DispatchJob.MessageContent,
        CampaignId = r.DispatchJob.CampaignId,
        SentAt = AttemptTime(r.DispatchJob),
        IsFallback = r.DispatchJob.IsFallback,
        ResentFromRecipientId = r.ResentFromRecipientId,
        RawProviderResponse = ResolveRawResponse(r)
    };

    /// <summary>
    /// The fix for the "wrong provider reply" bug: a recipient's own RawStatusResponse
    /// stays null until the next status-sync run (and can stay null forever for an
    /// unmatched Voice call), even though the job-level ProviderRawResponse was written
    /// immediately at dispatch time. Falling back to the job avoids a blank tooltip while
    /// still showing genuine provider data, not a placeholder.
    /// </summary>
    /// <summary>
    /// When this recipient's send was attempted. A job's SentAt is only set when the provider
    /// ACCEPTED it, so every failed row showed a blank Sent At. The job is created immediately before
    /// its send, and a resend creates a fresh job, so its creation time is the attempt time and moves
    /// forward on every resend.
    /// </summary>
    public static DateTimeOffset AttemptTime(DispatchJob job) => job.SentAt ?? job.CreatedAt;

    /// <summary>
    /// A failed row always has something to show. When the provider never replied (the send was
    /// refused before it was made, or the call threw) there is no provider text to display, so the
    /// row says so and gives the reason instead of an empty field.
    /// </summary>
    /// <summary>
    /// Marks a recipient row reconstructed by the one-time 2026-09-20 backfill migration
    /// (pre-R3 history that predates per-recipient tracking) — see its own doc comment.
    /// Deliberately not a genuine provider reference, so nobody mistakes a guess for
    /// confirmed delivery.
    /// </summary>
    private const string BackfilledSentinel = "backfilled";

    public static string? ResolveRawResponse(DispatchJobRecipient r)
    {
        // Hotfix batch 9, Task 17: the bug the team lead actually saw. A backfilled row's
        // own RawStatusResponse is deliberately NULL (nothing per-recipient was ever
        // captured for it), so ResolveProviderReply's job-level fallback below filled it
        // with the job's real raw response — which carries the job's real Message_ID
        // right next to a ProviderReferenceId field still showing the literal string
        // "backfilled". The two fields visibly contradicted each other on exactly the
        // rows the original migration meant to mark as an honest guess, not confirmed
        // data. Short-circuit before that fallback runs for these rows.
        if (r.ProviderReferenceId == BackfilledSentinel)
        {
            return JsonSerializer.Serialize(new
            {
                backfilled = true,
                note = "This row predates per-recipient tracking (reconstructed from the job "
                     + "on 2026-09-20) — the provider's exact reply for this specific number "
                     + "was never captured, so there is no raw response to show."
            });
        }

        var raw = ResolveProviderReply(r);
        if (raw is not null) return raw;

        var job = r.DispatchJob;
        var failed = r.DeliveryStatus == DeliveryStatus.Failed || job?.Status == JobStatus.Failed;
        if (!failed) return null;

        return JsonSerializer.Serialize(new
        {
            providerReplied = false,
            note = "The provider gave no reply for this send, so there is no raw response to show.",
            failureReason = string.IsNullOrWhiteSpace(job?.FailureReason) ? "No reason was recorded." : job!.FailureReason
        });
    }

    private static string? ResolveProviderReply(DispatchJobRecipient r)
    {
        if (!string.IsNullOrWhiteSpace(r.RawStatusResponse)) return r.RawStatusResponse;

        var jobRaw = r.DispatchJob?.ProviderRawResponse;
        if (string.IsNullOrWhiteSpace(jobRaw)) return null;

        // Chithi batches every recipient of a job into one array on the job row, one
        // element per call it actually made — pull out just this recipient's own entry
        // (matched by Msisdn) so the fallback still shows one call, not the whole batch.
        // Every other sender's job-level response is already a single object, so this
        // only ever matches when there is something to match.
        try
        {
            using var doc = JsonDocument.Parse(jobRaw);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var call in doc.RootElement.EnumerateArray())
                {
                    if (call.TryGetProperty("Msisdn", out var msisdn)
                        && msisdn.GetString() == r.Recipient)
                    {
                        return call.GetRawText();
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON (or unrecognised shape) — fall through and return it verbatim below;
            // it is still the exact raw text the provider returned.
        }

        return jobRaw;
    }

    public async Task<List<ResendAttemptDto>> GetAttemptHistoryAsync(Guid recipientId)
    {
        var attempts = await attemptRepo.GetByRecipientIdAsync(recipientId);
        return attempts.Select(a => new ResendAttemptDto
        {
            Id = a.Id,
            DeliveryStatus = a.DeliveryStatus,
            ProviderReferenceId = a.ProviderReferenceId,
            RawResponse = a.RawResponse,
            ChannelLabel = a.DispatchJob?.ChannelConfig?.Label,
            AttemptedAt = a.AttemptedAt
        }).ToList();
    }
}
