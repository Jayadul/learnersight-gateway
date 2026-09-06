using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Route("api/message-requests")]
public class MessageRequestController(IMessageRequestRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] string? toUser,
        [FromQuery] MessageStatus? status,
        [FromQuery] MessageSource? source,
        [FromQuery] Guid? channelConfigId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (items, total) = await repo.GetPagedAsync(
            page, pageSize, dateFrom, dateTo, toUser, status, source, channelConfigId);

        return Ok(new PagedResult<MessageRequestDto>
        {
            Items = items.Select(m => new MessageRequestDto
            {
                Id = m.Id,
                ToUser = m.ToUser,
                RawMessageContent = m.RawMessageContent,
                ParsedChannels = m.ParsedChannels,
                OverallStatus = m.OverallStatus,
                ReceivedAt = m.ReceivedAt,
                Source = m.Source,
                IsMultiChannel = m.IsMultiChannel,
                CreatedAt = m.CreatedAt
            }).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var m = await repo.GetWithJobsAsync(id);
        if (m is null) return NotFound();

        return Ok(new MessageRequestDetailDto
        {
            Id = m.Id,
            ToUser = m.ToUser,
            RawMessageContent = m.RawMessageContent,
            ParsedChannels = m.ParsedChannels,
            OverallStatus = m.OverallStatus,
            ReceivedAt = m.ReceivedAt,
            Source = m.Source,
            IsMultiChannel = m.IsMultiChannel,
            CreatedAt = m.CreatedAt,
            Jobs = m.Jobs.Select(j => new DispatchJobDto
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
                CreatedAt = j.CreatedAt,
                Recipients = j.Recipients
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => new DispatchJobRecipientDto
                    {
                        Id = r.Id,
                        DispatchJobId = r.DispatchJobId,
                        Recipient = r.Recipient,
                        ProviderReferenceId = r.ProviderReferenceId,
                        DeliveryStatus = r.DeliveryStatus,
                        LastCheckedAt = r.LastCheckedAt,
                        CreatedAt = r.CreatedAt
                    }).ToList()
            }).ToList()
        });
    }
}
