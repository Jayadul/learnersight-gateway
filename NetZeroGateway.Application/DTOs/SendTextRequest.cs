using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace NetZeroGateway.Application.DTOs;

public class SendTextRequest
{
    [FromForm(Name = "apikey")] public string? ApiKey { get; set; }
    [FromForm(Name = "secretkey")] public string? SecretKey { get; set; }
    [FromForm(Name = "callerID")] public string? CallerID { get; set; }
    [Required]
    [FromForm(Name = "toUser")] public string ToUser { get; set; } = string.Empty;
    [Required]
    [FromForm(Name = "messageContent")] public string MessageContent { get; set; } = string.Empty;
}

/// <summary>
/// Same fields as <see cref="SendTextRequest"/>, bound from the query string instead of
/// a form body, for <c>GET /sendtext</c>. A separate type because the binding-source
/// attributes on <see cref="SendTextRequest"/>'s properties pin it to form data
/// regardless of how the action parameter itself is decorated. Kept in step with
/// <see cref="SendTextRequest"/>'s validation so a missing required field behaves the
/// same on either verb.
/// </summary>
public class SendTextQueryRequest
{
    [FromQuery(Name = "apikey")] public string? ApiKey { get; set; }
    [FromQuery(Name = "secretkey")] public string? SecretKey { get; set; }
    [FromQuery(Name = "callerID")] public string? CallerID { get; set; }
    [Required]
    [FromQuery(Name = "toUser")] public string ToUser { get; set; } = string.Empty;
    [Required]
    [FromQuery(Name = "messageContent")] public string MessageContent { get; set; } = string.Empty;
}

/// <summary>
/// Same fields as <see cref="SendTextRequest"/>, bound from a JSON body instead of a form
/// body, for <c>POST /sendtext</c> with <c>Content-Type: application/json</c>. A separate
/// type for the same reason as <see cref="SendTextQueryRequest"/>: the binding-source
/// attributes on <see cref="SendTextRequest"/>'s properties pin it to form data.
/// </summary>
public class SendTextJsonRequest
{
    [JsonPropertyName("apikey")] public string? ApiKey { get; set; }
    [JsonPropertyName("secretkey")] public string? SecretKey { get; set; }
    [JsonPropertyName("callerID")] public string? CallerID { get; set; }
    [Required]
    [JsonPropertyName("toUser")] public string ToUser { get; set; } = string.Empty;
    [Required]
    [JsonPropertyName("messageContent")] public string MessageContent { get; set; } = string.Empty;
}
