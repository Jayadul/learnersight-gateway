using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface IManualSendService
{
    Task<ManualSendResponse> SendAsync(ManualSendRequest request);
}
