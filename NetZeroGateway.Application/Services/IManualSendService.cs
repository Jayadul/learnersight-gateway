using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface IManualSendService
{
    Task<ManualSendResponse> SendAsync(ManualSendRequest request);

    /// <summary>
    /// Admin-initiated WhatsApp Template send (hotfix batch 3). Synchronous, unlike
    /// <see cref="SendAsync"/>'s fire-and-forget dispatch: a single-recipient template
    /// call is fast enough to await, and doing so returns a definite sent/failed status
    /// immediately rather than always reading "queued".
    /// </summary>
    Task<ManualSendResponse> SendTemplateAsync(SendTemplateRequest request);
}
