using System.Text.Json.Serialization;

namespace NetZeroGateway.Infrastructure.HttpClients.Models;

/// <summary>
/// Chithi's send response. The vendor supplied no sample body, so this is modelled on
/// the shape its own status endpoint and GenNet's send both use. Everything is
/// nullable and the raw payload is persisted, so an unexpected shape shows up as a
/// readable failure rather than a parse exception.
/// </summary>
public class ChithiSendResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("status_code")] public int? StatusCode { get; set; }
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }

    // As with GenNet, a rejection explains itself here rather than in error_message —
    // "Insufficient SMS balance" arrives as description, and losing it leaves the
    // delivery log saying only "FAILED".
    [JsonPropertyName("status_meaning")] public string? StatusMeaning { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("smsinfo")] public List<ChithiSmsInfo>? SmsInfo { get; set; }

    [JsonIgnore]
    public bool IsSuccess =>
        string.Equals(Status, "SUCCESS", StringComparison.OrdinalIgnoreCase);

    /// <summary>The most specific explanation the response carries.</summary>
    [JsonIgnore]
    public string? Reason => Description ?? StatusMeaning ?? ErrorMessage ?? Status;

    /// <summary>Chithi sends one recipient per call, so there is at most one reference id.</summary>
    [JsonIgnore]
    public string? ReferenceId => SmsInfo?.FirstOrDefault()?.ReferenceId;
}

public class ChithiSmsInfo
{
    [JsonPropertyName("msisdn")] public string? Msisdn { get; set; }
    [JsonPropertyName("reference_id")] public string? ReferenceId { get; set; }
    [JsonPropertyName("sms_status")] public string? SmsStatus { get; set; }
}

/// <summary>Chithi's balance response. No credit-limit concept, as with GenNet.</summary>
public class ChithiBalanceResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }
    [JsonPropertyName("status_meaning")] public string? StatusMeaning { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("smsinfo")] public ChithiBalanceInfo? SmsInfo { get; set; }

    [JsonIgnore]
    public bool IsSuccess =>
        string.Equals(Status, "SUCCESS", StringComparison.OrdinalIgnoreCase);
}

public class ChithiBalanceInfo
{
    [JsonPropertyName("balance_info")] public List<ChithiBalanceEntry>? BalanceInfo { get; set; }
}

public class ChithiBalanceEntry
{
    [JsonPropertyName("sms_balance")] public decimal? SmsBalance { get; set; }
}
