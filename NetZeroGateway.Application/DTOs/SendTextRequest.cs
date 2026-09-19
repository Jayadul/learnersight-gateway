using System.ComponentModel.DataAnnotations;
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
