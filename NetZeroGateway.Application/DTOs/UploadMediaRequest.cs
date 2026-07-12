namespace NetZeroGateway.Application.DTOs;

/// <summary>
/// Upload input assembled by the controller from the multipart form —
/// Application stays free of ASP.NET types (no IFormFile here).
/// </summary>
public class UploadMediaRequest
{
    public Stream Content { get; set; } = Stream.Null;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? CustomName { get; set; }
    public int ExpiryDays { get; set; } = 90;
}
