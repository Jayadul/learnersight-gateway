using System.Text.Json.Serialization;

namespace NetZeroGateway.Infrastructure.HttpClients.Models;

/// <summary>
/// The voice combined-report. Carries no reference to anything we sent — records are
/// matched by destination number and start time, see NetZeroVoiceStatusSyncProvider.
/// </summary>
public class NetZeroVoiceReportResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("statusCode")] public long? StatusCode { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("totalRecords")] public int? TotalRecords { get; set; }
    [JsonPropertyName("data")] public List<NetZeroVoiceCallRecord>? Data { get; set; }
    [JsonPropertyName("records")] public List<NetZeroVoiceCallRecord>? Records { get; set; }

    [JsonIgnore] public string? Error { get; set; }

    /// <summary>
    /// A top-level failure means the whole request was rejected — an expired session,
    /// bad credentials. It says nothing about any call, so the caller must leave every
    /// recipient row untouched.
    /// </summary>
    [JsonIgnore]
    public bool IsRequestFailure =>
        Error is not null
        || string.Equals(Status, "failure", StringComparison.OrdinalIgnoreCase);

    /// <summary>The vendor's sample names the array `data`; `records` is a fallback.</summary>
    [JsonIgnore]
    public List<NetZeroVoiceCallRecord> Rows => Data ?? Records ?? [];
}

public class NetZeroVoiceCallRecord
{
    [JsonPropertyName("Destination")] public string? Destination { get; set; }
    [JsonPropertyName("StartTime")] public string? StartTime { get; set; }
    [JsonPropertyName("EndTime")] public string? EndTime { get; set; }
    [JsonPropertyName("CallState")] public string? CallState { get; set; }
    [JsonPropertyName("ConnectTime")] public string? ConnectTime { get; set; }
    [JsonPropertyName("DisplayNameSent")] public string? DisplayNameSent { get; set; }
}
