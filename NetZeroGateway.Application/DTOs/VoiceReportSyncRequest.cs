namespace NetZeroGateway.Application.DTOs;
public class VoiceReportSyncRequest
{
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
}
