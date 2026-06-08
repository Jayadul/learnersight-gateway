using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public interface INetZeroReportClient
{
    Task<VoiceLogResponse> GetCombinedReportAsync(
        string owner, string password,
        DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default);
}
