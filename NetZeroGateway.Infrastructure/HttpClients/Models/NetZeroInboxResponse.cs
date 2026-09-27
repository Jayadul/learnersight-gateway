using System.Text.Json.Serialization;

namespace NetZeroGateway.Infrastructure.HttpClients.Models;

/// <summary>
/// NetZero Inbox's v1 send response. Returns a WhatsApp message id (<c>wamid…</c>) per
/// message, captured even though the vendor documents no status endpoint yet — it costs
/// nothing to store and saves a re-integration if one appears.
/// </summary>
public class NetZeroInboxResponse
{
    [JsonPropertyName("success")] public bool? Success { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("data")] public NetZeroInboxData? Data { get; set; }

    /// <summary>The provider's reply exactly as received. Stored and shown as the raw response, instead of re-serialising this model, which added null fields for everything the model knows and dropped anything it does not.</summary>
    [JsonIgnore] public string? RawBody { get; set; }

    /// <summary>
    /// The vendor's success signal is not pinned down by the one sample we have, so
    /// treat any of the three plausible markers as success: an explicit success flag, a
    /// success status string, or a returned message id. A response carrying an error
    /// message is never success.
    /// </summary>
    [JsonIgnore]
    public bool IsSuccess =>
        string.IsNullOrWhiteSpace(Error)
        && (Success == true
            || string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(Data?.Id));

    /// <summary>The WhatsApp message id, when the vendor returned one.</summary>
    [JsonIgnore]
    public string? MessageId => Data?.Id;
}

public class NetZeroInboxData
{
    [JsonPropertyName("id")] public string? Id { get; set; }
}
