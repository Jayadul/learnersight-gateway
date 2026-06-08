using System.Text.Json.Serialization;

namespace NetZeroGateway.Infrastructure.HttpClients.Models;

public class ReveSmsResponse
{
    [JsonPropertyName("Status")]     public string  Status     { get; set; } = "";
    [JsonPropertyName("Text")]       public string  Text       { get; set; } = "";
    [JsonPropertyName("Message_ID")] public string? Message_ID { get; set; }
}
