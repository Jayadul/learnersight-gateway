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
