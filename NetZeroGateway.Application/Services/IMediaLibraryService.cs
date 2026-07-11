using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface IMediaLibraryService
{
    Task<MediaFileDto> UploadAsync(UploadMediaRequest request, string uploadedBy, CancellationToken ct = default);
    Task<PagedResult<MediaFileDto>> GetPagedAsync(int page, int size, string? search);
    Task<MediaFileDto> GetByIdAsync(Guid id);
    Task<(Stream Content, string ContentType, string DownloadName)> OpenPublicFileAsync(string storedFileName);
    Task DeleteAsync(Guid id, string deletedBy);
    Task CleanupExpiredAsync();
}
