namespace NetZeroGateway.Application.DTOs;

/// <summary>Parameters for a manual sync run, from the channel card's "Sync now" dialog.</summary>
public class StatusSyncRequest
{
    /// <summary>Start of the window. Defaults to the previous full day when omitted.</summary>
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }

    /// <summary>
    /// Most recipient rows to resolve in one run. Bounded so a manual sync over a wide
    /// window cannot spend hours calling a provider once per row.
    /// </summary>
    public int? Limit { get; set; }
}
