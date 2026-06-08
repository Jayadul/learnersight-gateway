using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Interfaces;

/// <summary>
/// Application-layer abstraction over the external voice report API.
/// Infrastructure implements this via NetZeroReportClient.
/// Keeps Application clean of Infrastructure/HTTP concerns.
/// </summary>
public interface IVoiceReportGateway
{
    Task<VoiceReportSyncResponse> GetCombinedReportAsync(
        string        owner,
        string        password,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}
