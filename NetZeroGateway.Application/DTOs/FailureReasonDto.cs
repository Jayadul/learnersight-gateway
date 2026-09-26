namespace NetZeroGateway.Application.DTOs;

public class FailureReasonDto
{
    public string Reason { get; set; } = string.Empty;
    /// <summary>Failures across every channel — the sum of <see cref="ByChannel"/>.</summary>
    public int Count { get; set; }

    /// <summary>
    /// The same failures split by channel, worst first. Reason alone could not tell a
    /// provider outage on one channel from a problem affecting all of them.
    /// </summary>
    public List<ChannelFailureCountDto> ByChannel { get; set; } = [];
}

public class ChannelFailureCountDto
{
    public string ChannelType { get; set; } = string.Empty;
    /// <summary>Which gateway, when the job still has one. Several may be live per type.</summary>
    public string? ChannelLabel { get; set; }
    /// <summary>The gateway's id, so a click on this pill lists exactly this gateway's failures, not every channel of the type.</summary>
    public Guid ChannelConfigId { get; set; }
    public int Count { get; set; }
}
