using System.Text.Json.Serialization;
namespace NetZeroGateway.Application.DTOs;
public class SendTextResponse
{
    [JsonPropertyName("Status")]     public string  Status     { get; set; } = "0";
    [JsonPropertyName("Text")]       public string  Text       { get; set; } = "ACCEPTD";
    [JsonPropertyName("Message_ID")] public string? Message_ID { get; set; }
    public static SendTextResponse Accepted(Guid id) =>
        new() { Status = "0", Text = "ACCEPTD", Message_ID = id.ToString() };
    public static SendTextResponse Rejected() =>
        new() { Status = "101", Text = "REJECTD" };
}
