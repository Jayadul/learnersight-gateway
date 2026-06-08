using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface ISendTextOrchestratorService
{
    Task<SendTextResponse> ProcessAsync(SendTextRequest request);
}
