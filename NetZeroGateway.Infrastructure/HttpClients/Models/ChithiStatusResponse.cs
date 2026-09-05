using System.Text.Json.Serialization;

namespace NetZeroGateway.Infrastructure.HttpClients.Models;

/// <summary>
/// Chithi's per-reference status lookup. Two failure shapes must be told apart: a
/// populated <c>smsinfo</c> whose <c>sms_status</c> is not Success is a delivery
/// failure, while a top-level FAILED with an empty <c>smsinfo</c> means the lookup
/// itself was rejected and says nothing about the message.
/// </summary>
public class ChithiStatusResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("status_code")] public int? StatusCode { get; set; }
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }
    [JsonPropertyName("smsinfo")] public List<ChithiStatusInfo>? SmsInfo { get; set; }

    [JsonIgnore] public string? Error { get; set; }

    /// <summary>The lookup was rejected rather than answered — a stale reference id.</summary>
    [JsonIgnore]
    public bool IsLookupRejected =>
        string.Equals(Status, "FAILED", StringComparison.OrdinalIgnoreCase)
        && (SmsInfo is null || SmsInfo.Count == 0);

    [JsonIgnore] public string? DeliveryState => SmsInfo?.FirstOrDefault()?.SmsStatus;
}

public class ChithiStatusInfo
{
    [JsonPropertyName("msisdn")] public string? Msisdn { get; set; }
    [JsonPropertyName("reference_id")] public string? ReferenceId { get; set; }
    [JsonPropertyName("sms_status")] public string? SmsStatus { get; set; }
}
