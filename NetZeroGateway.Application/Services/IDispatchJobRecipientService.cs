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
}
