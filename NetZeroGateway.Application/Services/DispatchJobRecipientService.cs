using System.Text.Json;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public class DispatchJobRecipientService(IDispatchJobRecipientRepository repo)
    : IDispatchJobRecipientService
{
    public async Task<PagedResult<DispatchJobRecipientDto>> GetPagedAsync(
        int page, int pageSize,
        DateTimeOffset? dateFrom, DateTimeOffset? dateTo,
        ChannelType? channelType, Guid? channelConfigId,
        DeliveryStatus? deliveryStatus, string? recipient)
    {
        var (items, total) = await repo.GetPagedAsync(
            page, pageSize, dateFrom, dateTo, channelType, channelConfigId, deliveryStatus, recipient);

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
        ChannelType = r.DispatchJob!.ChannelType,
        ChannelLabel = r.DispatchJob.ChannelConfig?.Label,
        MessageContent = r.DispatchJob.MessageContent,
        CampaignId = r.DispatchJob.CampaignId,
        SentAt = r.DispatchJob.SentAt,
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
    private static string? ResolveRawResponse(DispatchJobRecipient r)
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
}
