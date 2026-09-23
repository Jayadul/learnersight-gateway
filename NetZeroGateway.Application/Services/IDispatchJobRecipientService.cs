using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

/// <summary>Read side of the merged, per-recipient Message Logs page (hotfix batch 3).</summary>
public interface IDispatchJobRecipientService
{
    Task<PagedResult<DispatchJobRecipientDto>> GetPagedAsync(
        int page, int pageSize,
        DateTimeOffset? dateFrom, DateTimeOffset? dateTo,
        ChannelType? channelType, Guid? channelConfigId,
        DeliveryStatus? deliveryStatus, string? recipient,
        string? failureReason = null);

    /// <summary>
    /// A recipient's resend history, most recent first (hotfix batch 4, task 2a) — each
    /// attempt's own outcome, since the main row only ever shows the latest one.
    /// </summary>
    Task<List<ResendAttemptDto>> GetAttemptHistoryAsync(Guid recipientId);
}
