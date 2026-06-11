using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public class DashboardService(
    IMessageRequestRepository msgRepo,
    IDispatchJobRepository jobRepo
) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        var todayStart = DateTimeOffset.UtcNow.Date;
        var weekStart = todayStart.AddDays(-6);

        var allRequests = await msgRepo.GetAllAsync();
        var allJobs = await jobRepo.GetAllAsync();

        var totalToday = allRequests.Count(m => m.ReceivedAt >= todayStart);
        var totalWeek = allRequests.Count(m => m.ReceivedAt >= weekStart);

        var jobsToday = allJobs.Where(j => j.CreatedAt >= todayStart).ToList();
        var smsSent = jobsToday.Count(j => j.ChannelType == ChannelType.SMS && j.Status == JobStatus.Sent);
        var wpSent = jobsToday.Count(j => (j.ChannelType == ChannelType.WP_Text ||
                                                  j.ChannelType == ChannelType.WP_Media) && j.Status == JobStatus.Sent);
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
        var from = DateTimeOffset.UtcNow.Date.AddDays(-(days - 1));
        var items = await msgRepo.FindAsync(m => m.ReceivedAt >= from);

        return items
            .GroupBy(m => DateOnly.FromDateTime(m.ReceivedAt.LocalDateTime))
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

    public async Task<List<FailureReasonDto>> GetTopFailureReasonsAsync(int top = 5)
    {
        var failedJobs = await jobRepo.FindAsync(
            j => j.Status == JobStatus.Failed && j.FailureReason != null);

        return failedJobs
            .GroupBy(j => j.FailureReason!)
            .OrderByDescending(g => g.Count())
            .Take(top)
            .Select(g => new FailureReasonDto { Reason = g.Key, Count = g.Count() })
            .ToList();
    }
}
