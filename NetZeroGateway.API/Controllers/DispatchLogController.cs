using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Route("api/dispatch-logs")]
public class DispatchLogController(IDispatchJobRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] ChannelType? channelType,
        [FromQuery] JobStatus? status,
        [FromQuery] string? toNumber,
        [FromQuery] DeliveryStatus? deliveryStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (items, total) = await repo.GetPagedAsync(
            page, pageSize, dateFrom, dateTo, channelType, status, toNumber, deliveryStatus);

        return Ok(new PagedResult<DispatchJobDto>
        {
            Items = items.Select(j => new DispatchJobDto
            {
                Id = j.Id,
                ChannelType = j.ChannelType,
                ChannelLabel = j.ChannelConfig?.Label,
                ToNumber = j.ToNumber,
                MessageContent = j.MessageContent,
                Status = j.Status,
                ProviderMessageId = j.ProviderMessageId,
                ProviderRawResponse = j.ProviderRawResponse,
                FailureReason = j.FailureReason,
                SentAt = j.SentAt,
                IsFallback = j.IsFallback,
                CampaignId = j.CampaignId,
                FallbackFromJobId = j.FallbackFromJobId,
                CreatedAt = j.CreatedAt
            }).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }
}
