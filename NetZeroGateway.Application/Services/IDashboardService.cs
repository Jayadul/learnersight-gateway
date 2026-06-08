using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto>      GetSummaryAsync();
    Task<List<TimelinePointDto>>   GetTimelineAsync(int days);
    Task<List<ChannelBreakdownDto>> GetChannelBreakdownAsync();
    Task<List<FailureReasonDto>>   GetTopFailureReasonsAsync(int top = 5);
}
