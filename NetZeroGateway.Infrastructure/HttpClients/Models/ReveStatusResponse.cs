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
}
