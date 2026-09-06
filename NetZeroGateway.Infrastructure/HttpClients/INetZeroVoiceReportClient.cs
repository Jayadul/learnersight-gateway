using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public interface INetZeroVoiceReportClient
{
    /// <summary>
    /// Pulls the combined call report for a time window, paging until the vendor's
    /// totalRecords is exhausted. Uses the Voice channel's existing owner/password.
    /// </summary>
    Task<NetZeroVoiceReportResponse> GetCombinedReportAsync(
        string owner, string password,
        DateTimeOffset from, DateTimeOffset to,
        int offset, int limit,
        string? baseUrl = null,
        CancellationToken ct = default);
}
