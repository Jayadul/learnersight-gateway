using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Domain.Entities;

public class MediaFile : BaseEntity
{
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public MediaStatus Status { get; set; } = MediaStatus.Active;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
