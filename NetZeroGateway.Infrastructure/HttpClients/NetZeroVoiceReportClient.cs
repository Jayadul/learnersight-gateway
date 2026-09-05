using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using System.Text.Json;
using System.Web;

namespace NetZeroGateway.Infrastructure.HttpClients;

public class NetZeroVoiceReportClient(
    HttpClient http, IConfiguration config, ILogger<NetZeroVoiceReportClient> logger)
    : INetZeroVoiceReportClient
{
    private string ReportPath =>
        config["Providers:Voice:ReportPath"] ?? "/api/v1/combined-report";

    /// <summary>
    /// The report's fromTime/toTime are business-zone wall clock, not UTC — every other
    /// timestamp the provider shows is local, and sending UTC silently returns the wrong
    /// six hours of calls rather than an error.
    /// </summary>
    private const string ReportTimeFormat = "yyyy-MM-dd HH:mm:ss";

    public async Task<NetZeroVoiceReportResponse> GetCombinedReportAsync(
        string owner, string password,
        DateTimeOffset from, DateTimeOffset to,
        int offset, int limit,
        string? baseUrl = null, CancellationToken ct = default)
    {
        var tz = AppTime.Resolve(config);

        var q = HttpUtility.ParseQueryString(string.Empty);
        q["service"] = "campaign";
        q["actionId"] = "getCombinedDetailedReport";
        q["aggregationType"] = "Detailed";
        q["contType"] = "SuccessfulAndFailed";
        q["fromTime"] = AppTime.ToAppTime(from, tz).ToString(ReportTimeFormat);
        q["toTime"] = AppTime.ToAppTime(to, tz).ToString(ReportTimeFormat);
        q["download"] = "false";
        q["owner"] = owner;
        q["password"] = password;
        q["offset"] = offset.ToString();
        q["limit"] = limit.ToString();

        // Repeated fld parameters select the columns; ParseQueryString renders repeats
        // as fld=a&fld=b, which is the form the vendor's own example uses.
        foreach (var fld in new[]
                 { "StartTime", "Destination", "CallState", "DisplayNameSent", "ConnectTime", "EndTime" })
        {
            q.Add("fld", fld);
        }

        var url = ProviderEndpoint.Resolve(baseUrl, $"{ReportPath}?{q}");
        // owner/password are in the query string, so the URL is never logged.
        logger.LogInformation(
            "[VOICE_REPORT] GET {Path} window={From}..{To} offset={Offset} limit={Limit}",
            ReportPath, q["fromTime"], q["toTime"], offset, limit);

        var resp = await http.GetAsync(url, ct);
        var body = (await resp.Content.ReadAsStringAsync(ct)).Trim();

        if (string.IsNullOrWhiteSpace(body))
            return new NetZeroVoiceReportResponse { Error = "empty_response" };

        try
        {
            return JsonSerializer.Deserialize<NetZeroVoiceReportResponse>(body)
                ?? new NetZeroVoiceReportResponse { Error = "unreadable_response" };
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[VOICE_REPORT] Parse error. Body={Body}", body);
            return new NetZeroVoiceReportResponse { Error = ex.Message };
        }
    }
}
