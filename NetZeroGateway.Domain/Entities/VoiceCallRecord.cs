using System.Text.Json.Serialization;

namespace NetZeroGateway.Domain.Entities;

/// <summary>
/// Record returned by the NetZero voice combined-report API.
/// Kept in Domain so Application and API layers can reference it without touching Infrastructure.
/// </summary>
public class VoiceCallRecord
{
    [JsonPropertyName("CallState")]       public string? CallState       { get; set; }
    [JsonPropertyName("ConnectTime")]     public string? ConnectTime     { get; set; }
    [JsonPropertyName("Destination")]     public string? Destination     { get; set; }
    [JsonPropertyName("DisplayNameSent")] public string? DisplayNameSent { get; set; }
    [JsonPropertyName("EndTime")]         public string? EndTime         { get; set; }
    [JsonPropertyName("StartTime")]       public string? StartTime       { get; set; }
}
