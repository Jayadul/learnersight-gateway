using System.Text.Json.Serialization;
namespace NetZeroGateway.Application.DTOs;
public class SendTextResponse
{
    [JsonPropertyName("Status")] public string Status { get; set; } = "0";
    [JsonPropertyName("Text")] public string Text { get; set; } = "ACCEPTD";
    [JsonPropertyName("Message_ID")] public string? Message_ID { get; set; }

    /// <summary>
    /// Why the request was rejected, when the reason is the caller's to fix. Omitted from
    /// the JSON when null, so the REVE-compatible shape is unchanged for every other case.
    /// </summary>
    [JsonPropertyName("Reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; set; }

    public static SendTextResponse Accepted(Guid id) =>
        new() { Status = "0", Text = "ACCEPTD", Message_ID = id.ToString() };
    public static SendTextResponse Rejected(string? reason = null) =>
        new() { Status = "101", Text = "REJECTD", Reason = reason };
}
