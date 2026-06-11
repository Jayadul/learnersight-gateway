namespace NetZeroGateway.Application.DTOs;
public class ChannelBreakdownDto
{
    public string ChannelType { get; set; } = string.Empty;
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
}
