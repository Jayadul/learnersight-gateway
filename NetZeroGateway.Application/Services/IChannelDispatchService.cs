using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface IChannelDispatchService
{
    Task DispatchAsync(Guid messageRequestId, string toUser, List<ParsedChannelJob> jobs);
}
