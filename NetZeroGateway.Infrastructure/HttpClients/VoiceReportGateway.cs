using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Infrastructure.HttpClients;

/// <summary>
/// Infrastructure adapter: implements Application's IVoiceReportGateway
/// by delegating to INetZeroReportClient (HTTP).
/// </summary>
public class VoiceReportGateway(INetZeroReportClient client) : IVoiceReportGateway
{
    public async Task<VoiceReportSyncResponse> GetCombinedReportAsync(
        string owner, string password,
        DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default)
    {
        var result = await client.GetCombinedReportAsync(owner, password, from, to, ct);
        return new VoiceReportSyncResponse
        {
            TotalRecords = result.TotalRecords,
            Records      = result.Records
        };
    }
}
