namespace NetZeroGateway.Application.DTOs;

/// <summary>What one channel's sync run did, for the admin UI and the job log.</summary>
public class StatusSyncResultDto
{
    public Guid ChannelConfigId { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool Success { get; set; }

    /// <summary>Rows whose delivery status was resolved.</summary>
    public int Updated { get; set; }

    /// <summary>
    /// Rows the provider had nothing to say about. Normal for a message still in
    /// flight; a persistently high number is what a wrong correlation rule looks like.
    /// </summary>
    public int Unmatched { get; set; }

    /// <summary>Why the whole run failed. When set, no row was touched.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// Sync Now took the maximum for one run and more recipients are still waiting; running it
    /// again continues. False means everything waiting was asked about.
    /// </summary>
    public bool MoreWaiting { get; set; }

    /// <summary>The span the recipients asked about were sent in (oldest to newest).</summary>
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
}
