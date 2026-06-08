using Microsoft.AspNetCore.Mvc;
namespace NetZeroGateway.Application.DTOs;
public class SendTextRequest
{
    [FromForm(Name = "apikey")]         public string ApiKey         { get; set; } = string.Empty;
    [FromForm(Name = "secretkey")]      public string SecretKey      { get; set; } = string.Empty;
    [FromForm(Name = "callerID")]       public string CallerID       { get; set; } = string.Empty;
    [FromForm(Name = "toUser")]         public string ToUser         { get; set; } = string.Empty;
    [FromForm(Name = "messageContent")] public string MessageContent { get; set; } = string.Empty;
}
