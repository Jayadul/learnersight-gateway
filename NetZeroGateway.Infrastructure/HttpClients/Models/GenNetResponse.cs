using System.Text.Json.Serialization;

namespace NetZeroGateway.Infrastructure.HttpClients.Models;

/// <summary>
/// GenNet's bulk send response. The <c>smsinfo</c> array carries a status and a
/// reference id per recipient, which is richer than the job-level status the gateway
/// reports. The detail is persisted rather than discarded — see DispatchJobRecipient.
/// </summary>
public class GenNetSendResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("status_code")] public int? StatusCode { get; set; }
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }
    [JsonPropertyName("smsinfo")] public List<GenNetSmsInfo>? SmsInfo { get; set; }

    /// <summary>The vendor signals success with the literal string SUCCESS.</summary>
    [JsonIgnore]
    public bool IsSuccess =>
        string.Equals(Status, "SUCCESS", StringComparison.OrdinalIgnoreCase);
}

public class GenNetSmsInfo
{
    [JsonPropertyName("msisdn")] public string? Msisdn { get; set; }
    [JsonPropertyName("reference_id")] public string? ReferenceId { get; set; }
    [JsonPropertyName("sms_status")] public string? SmsStatus { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
}

/// <summary>
/// GenNet's balance response nests the figure under <c>smsinfo.balance_info[0]</c>.
/// There is no credit-limit concept, unlike REVE.
/// </summary>
public class GenNetBalanceResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }
    [JsonPropertyName("smsinfo")] public GenNetBalanceInfo? SmsInfo { get; set; }

    [JsonIgnore]
    public bool IsSuccess =>
        string.Equals(Status, "SUCCESS", StringComparison.OrdinalIgnoreCase);
}

public class GenNetBalanceInfo
{
    [JsonPropertyName("balance_info")] public List<GenNetBalanceEntry>? BalanceInfo { get; set; }
}

public class GenNetBalanceEntry
{
    [JsonPropertyName("sms_balance")] public decimal? SmsBalance { get; set; }
}
