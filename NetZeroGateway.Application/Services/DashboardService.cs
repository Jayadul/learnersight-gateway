using Microsoft.Extensions.Configuration;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public class DashboardService(
    IMessageRequestRepository msgRepo,
    IDispatchJobRepository jobRepo,
    IDispatchJobRecipientRepository recipientRepo,
    IConfiguration config
) : IDashboardService
{
    // "Today" is the business day (Dhaka), not the UTC day: on the UTC day the dashboard's
    // counters reset at 06:00 local time.
    private TimeZoneInfo Zone => AppTime.Resolve(config);

    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        var todayStart = AppTime.StartOfDayUtc(DateTimeOffset.UtcNow, Zone);
        var weekStart = todayStart.AddDays(-6);

        var allRequests = await msgRepo.GetAllAsync();
        var allJobs = await jobRepo.GetAllAsync();

        var totalToday = allRequests.Count(m => m.ReceivedAt >= todayStart);
        var totalWeek = allRequests.Count(m => m.ReceivedAt >= weekStart);

        var jobsToday = allJobs.Where(j => j.CreatedAt >= todayStart).ToList();
        var smsSent = jobsToday.Count(j => j.ChannelType == ChannelType.SMS && j.Status == JobStatus.Sent);
        var wpSent = jobsToday.Count(j => j.ChannelType == ChannelType.WP && j.Status == JobStatus.Sent);
        var voiceSent = jobsToday.Count(j => j.ChannelType == ChannelType.Voice && j.Status == JobStatus.Sent);
        var failed = jobsToday.Count(j => j.Status == JobStatus.Failed);
        var total = jobsToday.Count;
        var sent = jobsToday.Count(j => j.Status == JobStatus.Sent);
        var rate = total > 0 ? Math.Round((double)sent / total * 100, 1) : 0.0;

        return new DashboardSummaryDto
        {
            TotalToday = totalToday,
            TotalWeek = totalWeek,
            SmsSentToday = smsSent,
            WpSentToday = wpSent,
            VoiceSentToday = voiceSent,
            FailedToday = failed,
            SuccessRatePercent = rate
        };
    }

    public async Task<List<TimelinePointDto>> GetTimelineAsync(int days)
    {
        var zone = Zone;
        var from = AppTime.StartOfDayUtc(DateTimeOffset.UtcNow, zone).AddDays(-(days - 1));
        var items = await msgRepo.FindAsync(m => m.ReceivedAt >= from);

        return items
            .GroupBy(m => DateOnly.FromDateTime(AppTime.ToAppTime(m.ReceivedAt, zone).DateTime))
            .Select(g => new TimelinePointDto { Date = g.Key, Count = g.Count() })
            .OrderBy(p => p.Date)
            .ToList();
    }

    public async Task<List<ChannelBreakdownDto>> GetChannelBreakdownAsync()
    {
        var jobs = await jobRepo.GetAllAsync();
        return jobs
            .GroupBy(j => j.ChannelType)
            .Select(g => new ChannelBreakdownDto
            {
                ChannelType = g.Key.ToString(),
                SentCount = g.Count(j => j.Status == JobStatus.Sent),
                FailedCount = g.Count(j => j.Status == JobStatus.Failed)
            }).ToList();
    }

    /// <summary>
    /// Counts failed recipient rows, the same rows the chart's drill-down lists. See
    /// <see cref="IDispatchJobRecipientRepository.GetFailureReasonCountsAsync"/>.
    /// </summary>
    public async Task<List<FailureReasonDto>> GetTopFailureReasonsAsync(int top = 5)
        => BuildFailureReasons(await recipientRepo.GetFailureReasonCountsAsync(), top);

    public static List<FailureReasonDto> BuildFailureReasons(IEnumerable<FailureReasonRow> rows, int top)
        => rows
            .GroupBy(r => r.Reason)
            .OrderByDescending(g => g.Sum(r => r.Count))
            .Take(top)
            .Select(g => new FailureReasonDto
            {
                Reason = g.Key,
                Count = g.Sum(r => r.Count),
                ByChannel = g
                    .GroupBy(r => (r.ChannelType, r.ChannelConfigId, r.ChannelLabel))
                    .Select(c => new ChannelFailureCountDto
                    {
                        ChannelType = c.Key.ChannelType.ToString(),
                        ChannelConfigId = c.Key.ChannelConfigId,
                        ChannelLabel = c.Key.ChannelLabel,
                        Count = c.Sum(r => r.Count)
                    })
                    .OrderByDescending(c => c.Count)
                    .ToList()
            })
            .ToList();
}
