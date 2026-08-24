using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Interfaces;

public interface IMessageRequestRepository : IRepository<MessageRequest>
{
    Task<MessageRequest?> GetWithJobsAsync(Guid id);
    Task<(List<MessageRequest> Items, int Total)> GetPagedAsync(
        int page, int size,
        DateTimeOffset? from, DateTimeOffset? to,
        string? toUser, MessageStatus? status, MessageSource? source,
        Guid? channelConfigId);
}
