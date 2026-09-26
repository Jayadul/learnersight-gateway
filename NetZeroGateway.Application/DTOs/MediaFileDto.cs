using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.DTOs;

public class MediaFileDto
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public MediaStatus Status { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    /// <summary>
    /// Active and not yet expired, judged on the SERVER's clock. The admin page used to work
    /// this out from the browser's clock, so a phone with the wrong date refused to preview a
    /// perfectly good file.
    /// </summary>
    public bool IsAvailable { get; set; }
}
