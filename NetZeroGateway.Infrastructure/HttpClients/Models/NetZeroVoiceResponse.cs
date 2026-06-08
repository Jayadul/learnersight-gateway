using System.Text.Json.Serialization;

namespace NetZeroGateway.Infrastructure.HttpClients.Models;

public class NetZeroVoiceResponse
{
    [JsonPropertyName("message")]           public string? Message           { get; set; }
    [JsonPropertyName("resultUrl")]         public string? ResultUrl         { get; set; }
    [JsonPropertyName("status")]            public string? Status            { get; set; }
    [JsonPropertyName("statusUrl")]         public string? StatusUrl         { get; set; }
    [JsonPropertyName("taskId")]            public string? TaskId            { get; set; }
    [JsonPropertyName("totalDestinations")] public int     TotalDestinations { get; set; }
    /// <summary>status=="pending" means queued successfully (voice is async).</summary>
    public bool IsSuccess => Status == "pending";
}
