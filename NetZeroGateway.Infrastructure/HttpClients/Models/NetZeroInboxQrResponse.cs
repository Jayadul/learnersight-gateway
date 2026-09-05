using System.Text.Json.Serialization;

namespace NetZeroGateway.Infrastructure.HttpClients.Models;

/// <summary>
/// The QR device API's send response. The vendor supplied no sample body, so every
/// field is optional and the raw payload is persisted — an unexpected shape must read
/// as a plain failure rather than a parse exception.
/// </summary>
public class NetZeroInboxQrResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("success")] public bool? Success { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("messageId")] public string? MessageId { get; set; }

    /// <summary>
    /// Success is not pinned down by any sample, so accept the plausible markers and
    /// treat an error field as decisive against. Erring toward "failed" is the safer
    /// direction: a false failure is visible in the log, a false success is not.
    /// </summary>
    [JsonIgnore]
    public bool IsSuccess =>
        string.IsNullOrWhiteSpace(Error)
        && (Success == true
            || string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Status, "sent", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(Id)
            || !string.IsNullOrWhiteSpace(MessageId));

    [JsonIgnore] public string? ProviderMessageId => Id ?? MessageId;
}
