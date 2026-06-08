using System.Text.Json.Serialization;

namespace NetZeroGateway.Infrastructure.HttpClients.Models;

public class WaTrendMessageKey
{
    [JsonPropertyName("remoteJid")] public string RemoteJid { get; set; } = "";
    [JsonPropertyName("fromMe")]    public bool   FromMe    { get; set; }
    [JsonPropertyName("id")]        public string Id        { get; set; } = "";
}

public class WaTrendMessageDetail
{
    [JsonPropertyName("key")]              public WaTrendMessageKey? Key              { get; set; }
    [JsonPropertyName("messageTimestamp")] public string?            MessageTimestamp { get; set; }
    [JsonPropertyName("status")]           public string?            Status           { get; set; }
}

public class WaTrendResponse
{
    [JsonPropertyName("status")]  public string               Status  { get; set; } = "";
    [JsonPropertyName("message")] public WaTrendMessageDetail? Message { get; set; }
    public bool IsSuccess => Status == "success";
}
