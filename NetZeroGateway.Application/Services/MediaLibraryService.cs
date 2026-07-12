using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public class MediaLibraryService(
    IMediaFileRepository repo,
    IMediaStorageService storage,
    IConfiguration config,
    ILogger<MediaLibraryService> logger) : IMediaLibraryService
{
    private const int MaxExpiryDays = 3650;

    public async Task<MediaFileDto> UploadAsync(
        UploadMediaRequest request, string uploadedBy, CancellationToken ct = default)
    {
        Validate(request);

        var extension = Path.GetExtension(request.OriginalFileName);
        var baseName = string.IsNullOrWhiteSpace(request.CustomName)
            ? Path.GetFileNameWithoutExtension(request.OriginalFileName)
            : request.CustomName;

        var storedName = await MakeUniqueAsync(FileNameSlugger.Slugify(baseName, extension));
        var publicBase = (config[ConfigKeys.MediaPublicBaseUrl] ?? "").TrimEnd('/');

        await storage.SaveAsync(storedName, request.Content, ct);

        var entity = new MediaFile
        {
            OriginalFileName = request.OriginalFileName,
            StoredFileName = storedName,
            PublicUrl = $"{publicBase}/media/{storedName}",
            Extension = extension.TrimStart('.').ToLowerInvariant(),
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(request.ExpiryDays),
            Status = MediaStatus.Active,
            CreatedBy = uploadedBy
        };

        try
        {
            await repo.AddAsync(entity);
            await repo.SaveChangesAsync();
        }
        catch
        {
            storage.Delete(storedName); // don't leave an orphaned file behind
            throw;
        }

        logger.LogInformation(
            "Media uploaded: {StoredName} ({Size} bytes) by {User}, expires {ExpiresAt}",
            storedName, entity.SizeBytes, uploadedBy, entity.ExpiresAt);

        return ToDto(entity);
    }

    public async Task<PagedResult<MediaFileDto>> GetPagedAsync(int page, int size, string? search)
    {
        var (items, total) = await repo.GetPagedAsync(page, size, search);
        return new PagedResult<MediaFileDto>
        {
            Items = items.Select(ToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = size
        };
    }

    public async Task<MediaFileDto> GetByIdAsync(Guid id)
        => ToDto(await repo.GetByIdAsync(id) ?? throw new NotFoundException("File not found."));

    public async Task<(Stream Content, string ContentType, string DownloadName)> OpenPublicFileAsync(
        string storedFileName)
    {
        var media = await repo.GetByStoredNameAsync(storedFileName);
        if (media is null || media.Status != MediaStatus.Active || media.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new NotFoundException("This file is no longer available.");

        var stream = storage.OpenRead(storedFileName)
            ?? throw new NotFoundException("This file is no longer available.");

        return (stream, media.ContentType, media.OriginalFileName);
    }

    public async Task DeleteAsync(Guid id, string deletedBy)
    {
        var media = await repo.GetByIdAsync(id) ?? throw new NotFoundException("File not found.");
        if (media.Status == MediaStatus.Deleted)
            throw new InvalidOperationException("This file has already been deleted.");

        await DeleteFileAndMarkAsync(media, deletedBy);
        logger.LogInformation("Media deleted: {StoredName} by {User}", media.StoredFileName, deletedBy);
    }

    public async Task CleanupExpiredAsync()
    {
        var expired = await repo.GetExpiredActiveAsync(DateTimeOffset.UtcNow);
        if (expired.Count == 0)
        {
            logger.LogInformation("Media cleanup: no expired files.");
            return;
        }

        int deleted = 0, failed = 0;
        foreach (var media in expired)
        {
            try
            {
                await DeleteFileAndMarkAsync(media, "system");
                deleted++;
                logger.LogInformation("Media cleanup: deleted {StoredName} (expired {ExpiresAt})",
                    media.StoredFileName, media.ExpiresAt);
            }
            catch (Exception ex)
            {
                failed++; // keep going — one bad file must not block the rest
                logger.LogError(ex, "Media cleanup: failed to delete {StoredName}", media.StoredFileName);
            }
        }

        logger.LogInformation("Media cleanup finished: {Deleted} deleted, {Failed} failed", deleted, failed);
    }

    /// <summary>
    /// The single deletion path — manual delete and the expiry job both go through here:
    /// remove the physical file, keep the row, mark it Deleted with an audit trail.
    /// </summary>
    private async Task DeleteFileAndMarkAsync(MediaFile media, string deletedBy)
    {
        storage.Delete(media.StoredFileName);
        media.Status = MediaStatus.Deleted;
        media.DeletedAt = DateTimeOffset.UtcNow;
        media.DeletedBy = deletedBy;
        repo.Update(media);
        await repo.SaveChangesAsync();
    }

    private void Validate(UploadMediaRequest request)
    {
        if (request.SizeBytes <= 0)
            throw new InvalidOperationException("Please choose a file to upload.");

        var maxMb = config.GetValue(ConfigKeys.MediaMaxFileSizeMb, 25);
        if (request.SizeBytes > maxMb * 1024L * 1024L)
            throw new InvalidOperationException($"The file is too large. The maximum size is {maxMb} MB.");

        if (request.ExpiryDays < 1 || request.ExpiryDays > MaxExpiryDays)
            throw new InvalidOperationException($"Expiry must be between 1 and {MaxExpiryDays} days.");

        var extension = Path.GetExtension(request.OriginalFileName).TrimStart('.').ToLowerInvariant();
        if (extension.Length == 0)
            throw new InvalidOperationException("The file must have an extension, for example .png or .pdf.");

        var allowed = config.GetSection(ConfigKeys.MediaAllowedExtensions).Get<string[]>() ?? [];
        if (allowed.Length > 0 && !allowed.Contains(extension))
            throw new InvalidOperationException(
                $"Files of type .{extension} are not supported. Allowed types: {string.Join(", ", allowed)}.");
    }

    /// <summary>Duplicate names get a numeric suffix: company-logo.png → company-logo-2.png.</summary>
    private async Task<string> MakeUniqueAsync(string storedName)
    {
        if (!await repo.StoredNameExistsAsync(storedName)) return storedName;

        var extension = Path.GetExtension(storedName);
        var stem = Path.GetFileNameWithoutExtension(storedName);
        for (var i = 2; ; i++)
        {
            var candidate = $"{stem}-{i}{extension}";
            if (!await repo.StoredNameExistsAsync(candidate)) return candidate;
        }
    }

    private static MediaFileDto ToDto(MediaFile e) => new()
    {
        Id = e.Id,
        OriginalFileName = e.OriginalFileName,
        StoredFileName = e.StoredFileName,
        PublicUrl = e.PublicUrl,
        Extension = e.Extension,
        ContentType = e.ContentType,
        SizeBytes = e.SizeBytes,
        UploadedAt = e.CreatedAt,
        ExpiresAt = e.ExpiresAt,
        Status = e.Status,
        CreatedBy = e.CreatedBy,
        DeletedAt = e.DeletedAt,
        DeletedBy = e.DeletedBy
    };
}
