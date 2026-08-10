using System.Text.Json;
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

    /// <summary>
    /// Set when the provider returned <c>message</c> as a plain string instead of an
    /// object — that string is the human-readable failure reason.
    /// </summary>
    [JsonIgnore] public string? Text { get; set; }
}

/// <summary>
/// WaTrend returns <c>message</c> as an object on success but as a bare string on
/// failure (e.g. <c>"Instance ID Invalidated"</c>). Without this, the string form threw
/// a JsonException, the whole response was discarded, and the real reason never reached
/// the job's FailureReason — failures surfaced as an unexplained "error".
/// </summary>
public class WaTrendMessageConverter : JsonConverter<WaTrendMessageDetail?>
{
    public override WaTrendMessageDetail? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => new WaTrendMessageDetail { Text = reader.GetString() },
            // Applied per-property, so this type-level call does not re-enter the converter.
            _ => JsonSerializer.Deserialize<WaTrendMessageDetail>(ref reader, options)
        };

    public override void Write(
        Utf8JsonWriter writer, WaTrendMessageDetail? value, JsonSerializerOptions options)
    {
        // Round-trip the error string so ProviderRawResponse keeps the provider's wording.
        if (value?.Text is { } text) writer.WriteStringValue(text);
        else JsonSerializer.Serialize(writer, value, options);
    }
}

public class WaTrendResponse
{
    [JsonPropertyName("status")] public string Status { get; set; } = "";

    [JsonPropertyName("message")]
    [JsonConverter(typeof(WaTrendMessageConverter))]
    public WaTrendMessageDetail? Message { get; set; }

    public bool IsSuccess => Status == "success";

    /// <summary>
    /// The provider's own wording when it supplied one, else the bare status. Use this
    /// for a job's FailureReason so operators see "Instance ID Invalidated" rather than
    /// just "error".
    /// </summary>
    [JsonIgnore]
    public string FailureReason =>
        string.IsNullOrWhiteSpace(Message?.Text) ? Status : Message!.Text!;
}
