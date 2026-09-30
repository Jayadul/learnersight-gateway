using System.Text.Json.Serialization;

namespace NetZeroGateway.Infrastructure.HttpClients.Models;

/// <summary>
/// REVE's delivery-status lookup. <c>Text</c> carries the state (<c>DELIVRD</c>,
/// <c>REJECTD</c>, …) and <c>Status</c> a numeric code that is kept verbatim even when
/// unrecognised, so a state we have not seen is visible for review rather than silently
/// swallowed.
/// </summary>
public class ReveStatusResponse
{
    [JsonPropertyName("Status")] public string? Status { get; set; }
    [JsonPropertyName("Text")] public string? Text { get; set; }
    [JsonPropertyName("Message_ID")] public string? MessageId { get; set; }

    /// <summary>Unix milliseconds. "0" means no delivery time, not the epoch.</summary>
    [JsonPropertyName("Delivery Time")] public string? DeliveryTime { get; set; }

    [JsonIgnore] public string? Error { get; set; }

    /// <summary>
    /// REVE's own <c>getstatus</c> spec returns <c>Text: "REJECTD"</c> for six different
    /// <c>Status</c> codes (109 no apikey, 108 wrong password, 114 message id missing or
    /// already queried, 101 internal server error, -42 auth failed, 1 "request failed")
    /// — every one of them a rejection of the STATUS LOOKUP itself, never a statement
    /// about whether the original SMS was delivered. Confirmed live (hotfix batch 9,
    /// Task 4): a resend that REVE had genuinely accepted (ACCEPTD) later read back
    /// <c>{"Status":"114","Text":"REJECTD"}</c> — REVE re-asked about an id it had
    /// already answered — and the row was being recorded Failed on the strength of that
    /// alone. Same category of problem Chithi's aged-out reference ids already handle.
    /// </summary>
    [JsonIgnore]
    public bool IsLookupRejected =>
        string.Equals(Text?.Trim(), "REJECTD", StringComparison.OrdinalIgnoreCase);
}
