using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class MediaFileRepository(NetZeroGatewayDbContext db)
    : Repository<MediaFile>(db), IMediaFileRepository
{
    public async Task<(List<MediaFile> Items, int Total)> GetPagedAsync(
        int page, int size, string? search)
    {
        // Soft-deleted rows stay in the table for audit history but are never listed.
        var q = _set.Where(m => m.Status == MediaStatus.Active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(m => EF.Functions.ILike(m.OriginalFileName, pattern)
                          || EF.Functions.ILike(m.StoredFileName, pattern));
        }

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total);
    }

    public Task<bool> StoredNameExistsAsync(string storedFileName)
        => _set.AnyAsync(m => m.StoredFileName == storedFileName);

    public Task<MediaFile?> GetByStoredNameAsync(string storedFileName)
        => _set.FirstOrDefaultAsync(m => m.StoredFileName == storedFileName);

    public Task<List<MediaFile>> GetExpiredActiveAsync(DateTimeOffset asOf)
        => _set.Where(m => m.Status == MediaStatus.Active && m.ExpiresAt <= asOf).ToListAsync();
}
