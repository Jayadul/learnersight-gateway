using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Strategies;

/// <summary>
/// Asks one provider what became of messages it accepted, and writes the answers onto
/// the recipient rows.
/// <para>
/// A third capability alongside sending and balance, and just as independent: GenNet
/// accepts messages and reports a balance but exposes no status endpoint at all, so its
/// rows stay <see cref="DeliveryStatus.Pending"/> until it offers one. A channel type
/// never implies that a status sync exists.
/// </para>
/// </summary>
public interface IChannelStatusSyncProvider
{
    Provider Provider { get; }

    /// <summary>
    /// Resolves what it can among <paramref name="rows"/> and mutates them in place.
    /// The caller persists.
    /// <para>
    /// Two failure kinds must stay distinct. A recipient the provider says failed is a
    /// delivery failure and belongs on the row. A request the provider rejected outright
    /// — bad credentials, an expired session — is a sync failure that says nothing about
    /// any recipient, so every row must be left exactly as it was, including
    /// <see cref="DispatchJobRecipient.LastCheckedAt"/>. Conflating them writes
    /// "undelivered" against messages that arrived.
    /// </para>
    /// </summary>
    Task<StatusSyncResult> SyncAsync(
        Dictionary<string, string> config,
        List<DispatchJobRecipient> rows,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}

/// <summary>
/// The outcome of one channel's sync run. <paramref name="Unmatched"/> counts rows the
/// provider had nothing to say about — normal for a message still in flight, and the
/// figure to watch if a correlation rule is wrong.
/// </summary>
public record StatusSyncResult(
    bool Success,
    int Updated,
    int Unmatched,
    string? Error)
{
    public static StatusSyncResult Ok(int updated, int unmatched)
        => new(true, updated, unmatched, null);

    /// <summary>
    /// The whole request failed. No row was touched — see the remark on SyncAsync.
    /// </summary>
    public static StatusSyncResult Failed(string error) => new(false, 0, 0, error);
}
