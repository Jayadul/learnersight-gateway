using System.Text.Json.Serialization;

namespace NetZeroGateway.Infrastructure.HttpClients.Models;

/// <summary>
/// NetZero Inbox's send_templet response (hotfix batch 3). The vendor's own documented
/// shape only confirms one field worth keeping — metaResponse.message_id — so this models
/// exactly that, plus a top-level status/error pair on the same defensive footing every
/// other provider response here uses: an unrecognised or missing field never throws, and
/// success is inferred rather than assumed.
/// </summary>
public class NetZeroInboxTemplateResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("metaResponse")] public NetZeroInboxTemplateMetaResponse? MetaResponse { get; set; }

    /// <summary>
    /// No explicit success flag is documented, so this follows SendTextAsync's own rule:
    /// an explicit error is never success, and a returned message id is treated as proof
    /// the vendor accepted the template send.
    /// </summary>
    [JsonIgnore]
    public bool IsSuccess =>
        string.IsNullOrWhiteSpace(Error)
        && (string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(MetaResponse?.MessageId));

    [JsonIgnore]
    public string? MessageId => MetaResponse?.MessageId;
}

public class NetZeroInboxTemplateMetaResponse
{
    [JsonPropertyName("message_id")] public string? MessageId { get; set; }
}
