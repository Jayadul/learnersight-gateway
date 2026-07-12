using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Interfaces;

public interface IMediaFileRepository : IRepository<MediaFile>
{
    Task<(List<MediaFile> Items, int Total)> GetPagedAsync(int page, int size, string? search);
    Task<bool> StoredNameExistsAsync(string storedFileName);
    Task<MediaFile?> GetByStoredNameAsync(string storedFileName);
    Task<List<MediaFile>> GetExpiredActiveAsync(DateTimeOffset asOf);
}
