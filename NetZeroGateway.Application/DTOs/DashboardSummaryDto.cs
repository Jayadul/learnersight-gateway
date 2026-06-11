namespace NetZeroGateway.Application.DTOs;
public class DashboardSummaryDto
{
    public int TotalToday { get; set; }
    public int TotalWeek { get; set; }
    public int SmsSentToday { get; set; }
    public int WpSentToday { get; set; }
    public int VoiceSentToday { get; set; }
    public int FailedToday { get; set; }
    public double SuccessRatePercent { get; set; }
}
