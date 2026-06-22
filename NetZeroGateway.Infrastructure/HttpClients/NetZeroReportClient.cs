using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using System.Text.Json;

namespace NetZeroGateway.Infrastructure.HttpClients;

public class NetZeroReportClient(
    HttpClient http, IConfiguration config, ILogger<NetZeroReportClient> logger)
    : INetZeroReportClient
{
    private string ReportPath => config["Providers:VoiceReport:ReportPath"] ?? "/api/v1/combined-report";
    private string ActionId => config["Providers:VoiceReport:ActionId"] ?? "getCombinedDetailedReport";
    private string AggType => config["Providers:VoiceReport:AggregationType"] ?? "Detailed";
    private string ContType => config["Providers:VoiceReport:ContType"] ?? "SuccessfulAndFailed";
    private string Service => config["Providers:VoiceReport:Service"] ?? "campaign";
    private string DefaultOffset => config["Providers:VoiceReport:DefaultOffset"] ?? "0";
    private string DefaultLimit => config["Providers:VoiceReport:DefaultLimit"] ?? "100";
    private string Fields => config["Providers:VoiceReport:Fields"]
        ?? "StartTime,Destination,CallState,DisplayNameSent,ConnectTime,EndTime";

    private const string Fmt = "yyyy-MM-dd HH:mm:ss";

    public async Task<VoiceLogResponse> GetCombinedReportAsync(
        string owner, string password,
        DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default)
    {
        var qs = new Dictionary<string, string>
        {
            ["service"] = Service,
            ["actionId"] = ActionId,
            ["aggregationType"] = AggType,
            ["contType"] = ContType,
            ["fromTime"] = from.UtcDateTime.ToString(Fmt),
            ["toTime"] = to.UtcDateTime.ToString(Fmt),
            ["download"] = "false",
            ["owner"] = owner,
            ["password"] = password,
            ["offset"] = DefaultOffset,
            ["limit"] = DefaultLimit
        };

        var query = string.Join("&",
            qs.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));

        // Append repeated fld= params from comma-separated Fields config
        var fieldParams = string.Join("&",
            Fields.Split(',').Select(f => $"fld={Uri.EscapeDataString(f.Trim())}"));

        var url = $"{ReportPath}?{query}&{fieldParams}";

        logger.LogInformation("[VOICE_REPORT] GET {Path} from={From} to={To}", ReportPath, from, to);

        var resp = await http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        logger.LogDebug("[VOICE_REPORT] Response length={Len}", body.Length);

        try
        {
            return JsonSerializer.Deserialize<VoiceLogResponse>(body)
                ?? new VoiceLogResponse { Status = "error" };
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[VOICE_REPORT] Parse error");
            return new VoiceLogResponse { Status = "error" };
        }
    }
}