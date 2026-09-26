using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Interfaces;

public interface IDispatchJobRecipientRepository : IRepository<DispatchJobRecipient>
{
    /// <summary>Recipient rows for one job, in the order they were dispatched.</summary>
    Task<List<DispatchJobRecipient>> GetByDispatchJobIdAsync(Guid dispatchJobId);

    /// <summary>
    /// Paged, filterable recipient rows for the merged Message Logs page (hotfix batch 3)
    /// — the job and its channel config are eager-loaded since the page shows channel
    /// name/type and message content per row, not just the recipient's own fields.
    /// </summary>
    /// <param name="failureReason">
    /// Exact match on the underlying job's FailureReason — the dashboard's
    /// failure-reasons chart drill-down (hotfix batch 3, task 8) re-queries this endpoint
    /// with the exact string a bar represents, rather than the dashboard carrying its own
    /// copy of the matching rows.
    /// </param>
    Task<(List<DispatchJobRecipient> Items, int Total)> GetPagedAsync(
        int page, int size,
        DateTimeOffset? from, DateTimeOffset? to,
        ChannelType? channelType, Guid? channelConfigId,
        DeliveryStatus? deliveryStatus, string? recipient,
        string? failureReason = null);

    /// <summary>
    /// Adds a whole job's recipients in one round trip. Jobs routinely carry hundreds of
    /// numbers, so adding them one at a time would multiply the writes per send.
    /// </summary>
    Task AddRangeAsync(IEnumerable<DispatchJobRecipient> recipients);

    /// <summary>
    /// Rows on one channel still awaiting a delivery status, dispatched inside the
    /// window. Only Pending rows are returned: a row that already resolved, or that
    /// failed before the provider accepted it, has nothing left to sync. The job is
    /// eager-loaded because the sync needs its provider reference and dispatch time.
    /// </summary>
    Task<List<DispatchJobRecipient>> GetPendingForChannelAsync(
        Guid channelConfigId, DateTimeOffset from, DateTimeOffset to, int limit);

    /// <summary>
    /// Failed recipient rows grouped by the reason on their current job, channel and gateway —
    /// the dashboard's failure-reasons chart (hotfix batch 8). It counts the SAME rows the chart's
    /// drill-down lists (a recipient whose delivery status is Failed, filtered by its job's
    /// failure reason), so the number on a bar and the rows behind it always agree. It used to
    /// count failed jobs, which is a different unit: one failed batch of 900 was one on the chart
    /// and 900 in the drill-down, and a failed job whose recipient was since resent was counted
    /// with no row left to show.
    /// </summary>
    Task<List<FailureReasonRow>> GetFailureReasonCountsAsync()
        => throw new NotSupportedException();
}

/// <summary>One group of failed recipient rows: a reason on one channel of one gateway.</summary>
public record FailureReasonRow(string Reason, ChannelType ChannelType, Guid ChannelConfigId, string? ChannelLabel, int Count);
