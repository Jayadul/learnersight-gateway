using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface IVoiceReportService
{
    Task<VoiceReportSyncResponse> SyncAsync(VoiceReportSyncRequest request);
}
