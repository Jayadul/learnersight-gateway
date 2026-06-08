using System.Text.Json.Serialization;
using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Infrastructure.HttpClients.Models;

public class VoiceLogResponse
{
    [JsonPropertyName("records")]      public List<VoiceCallRecord> Records      { get; set; } = [];
    [JsonPropertyName("status")]       public string?               Status       { get; set; }
    [JsonPropertyName("statusCode")]   public int                   StatusCode   { get; set; }
    [JsonPropertyName("totalRecords")] public int                   TotalRecords { get; set; }
}
