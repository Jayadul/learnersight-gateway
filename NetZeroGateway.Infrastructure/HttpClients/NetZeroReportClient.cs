using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public class NetZeroReportClient(HttpClient http, ILogger<NetZeroReportClient> logger)
    : INetZeroReportClient
{
    private const string Fmt = "yyyy-MM-dd HH:mm:ss";

    public async Task<VoiceLogResponse> GetCombinedReportAsync(
        string owner, string password,
        DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default)
    {
        var qs = new Dictionary<string, string>
        {
            ["service"] = "campaign",
            ["actionId"] = "getCombinedDetailedReport",
            ["aggregationType"] = "Detailed",
            ["contType"] = "SuccessfulAndFailed",
            ["fromTime"] = from.UtcDateTime.ToString(Fmt),
            ["toTime"] = to.UtcDateTime.ToString(Fmt),
            ["download"] = "false",
            ["owner"] = owner,
            ["password"] = password,
            ["offset"] = "0",
            ["limit"] = "100"
        };
        var query = string.Join("&",
            qs.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"))
            + "&fld=StartTime&fld=Destination&fld=CallState&fld=DisplayNameSent&fld=ConnectTime&fld=EndTime";

        logger.LogInformation("[VOICE_REPORT] GET /api/v1/combined-report from={From} to={To}", from, to);

        var resp = await http.GetAsync($"/api/v1/combined-report?{query}", ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        logger.LogDebug("[VOICE_REPORT] Response length={Len}", body.Length);

        try
        {
            return JsonSerializer.Deserialize<VoiceLogResponse>(body)
                ?? new VoiceLogResponse { Status = "error" };
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[VOICE_REPORT] JSON parse error");
            return new VoiceLogResponse { Status = "error" };
        }
    }
}
