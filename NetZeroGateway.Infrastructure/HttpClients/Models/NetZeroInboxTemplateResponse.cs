using System.Text.Json.Serialization;

namespace NetZeroGateway.Infrastructure.HttpClients.Models;

/// <summary>
/// NetZero Inbox's send_templet response. Hotfix batch 4's real test against the vendor
/// found the actual shape does not match what batch 3 guessed from the vendor's own
/// documentation: a real success looks like
/// <c>{"success":true,"metaResponse":{"messages":[{"id":"wamid...","message_status":"accepted"}]}}</c>
/// — a boolean <c>success</c>, not a "success" status string, and the message id lives
/// inside a <c>messages</c> array, not a flat <c>metaResponse.message_id</c>. The earlier
/// shape silently read a genuine, delivered send as a failure.
/// </summary>
public class NetZeroInboxTemplateResponse
{
    [JsonPropertyName("success")] public bool? Success { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("metaResponse")] public NetZeroInboxTemplateMetaResponse? MetaResponse { get; set; }

    /// <summary>
    /// An explicit error is never success; otherwise a real message id in the messages
    /// array is proof the vendor accepted the template send.
    /// </summary>
    [JsonIgnore]
    public bool IsSuccess =>
        string.IsNullOrWhiteSpace(Error)
        && (Success == true || !string.IsNullOrWhiteSpace(MessageId));

    [JsonIgnore]
    public string? MessageId => MetaResponse?.Messages?.FirstOrDefault()?.Id;
}

public class NetZeroInboxTemplateMetaResponse
{
    [JsonPropertyName("messaging_product")] public string? MessagingProduct { get; set; }
    [JsonPropertyName("messages")] public List<NetZeroInboxTemplateMessage>? Messages { get; set; }
}

public class NetZeroInboxTemplateMessage
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("message_status")] public string? MessageStatus { get; set; }
}
