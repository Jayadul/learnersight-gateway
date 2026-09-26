using System.Linq.Expressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Tests;

/// <summary>
/// The Media Library used to hand back the URL stored at upload time, so a file uploaded
/// while Media:PublicBaseUrl was still the shipped localhost default kept an unreachable
/// link after the base URL was corrected.
/// </summary>
public class MediaPublicUrlTests
{
    private sealed class OneFileRepo(MediaFile file) : IMediaFileRepository
    {
        public Task<MediaFile?> GetByIdAsync(Guid id) => Task.FromResult<MediaFile?>(file);
        public Task<List<MediaFile>> GetAllAsync() => Task.FromResult(new List<MediaFile> { file });
        public Task<List<MediaFile>> FindAsync(Expression<Func<MediaFile, bool>> predicate) => throw new NotSupportedException();
        public Task AddAsync(MediaFile entity) => throw new NotSupportedException();
        public void Update(MediaFile entity) => throw new NotSupportedException();
        public void Delete(MediaFile entity) => throw new NotSupportedException();
        public Task<int> SaveChangesAsync() => throw new NotSupportedException();
        public Task<(List<MediaFile> Items, int Total)> GetPagedAsync(int page, int size, string? search)
            => Task.FromResult((new List<MediaFile> { file }, 1));
        public Task<bool> StoredNameExistsAsync(string storedFileName) => throw new NotSupportedException();
        public Task<MediaFile?> GetByStoredNameAsync(string storedFileName) => throw new NotSupportedException();
        public Task<List<MediaFile>> GetExpiredActiveAsync(DateTimeOffset asOf) => throw new NotSupportedException();
    }

    private sealed class NoStorage : IMediaStorageService
    {
        public Task SaveAsync(string storedFileName, Stream content, CancellationToken ct = default) => throw new NotSupportedException();
        public Stream? OpenRead(string storedFileName) => throw new NotSupportedException();
        public void Delete(string storedFileName) => throw new NotSupportedException();
    }

    private static MediaLibraryService Build(string? baseUrl, MediaFile file)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(baseUrl is null
                ? []
                : new Dictionary<string, string?> { ["Media:PublicBaseUrl"] = baseUrl })
            .Build();
        return new MediaLibraryService(new OneFileRepo(file), new NoStorage(), config, NullLogger<MediaLibraryService>.Instance);
    }

    private static MediaFile StoredWithLocalhostUrl() => new()
    {
        StoredFileName = "invoice.pdf",
        PublicUrl = "http://localhost:18080/media/invoice.pdf"
    };

    [Fact]
    public async Task A_file_uploaded_under_a_stale_base_url_gets_the_current_one()
    {
        var service = Build("https://media.example.com", StoredWithLocalhostUrl());

        var dto = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Equal("https://media.example.com/media/invoice.pdf", dto.PublicUrl);
    }

    [Fact]
    public async Task Availability_is_judged_on_the_servers_clock()
    {
        var live = StoredWithLocalhostUrl();
        live.Status = NetZeroGateway.Domain.Enums.MediaStatus.Active;
        live.ExpiresAt = DateTimeOffset.UtcNow.AddDays(3);
        var expired = StoredWithLocalhostUrl();
        expired.Status = NetZeroGateway.Domain.Enums.MediaStatus.Active;
        expired.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var deleted = StoredWithLocalhostUrl();
        deleted.Status = NetZeroGateway.Domain.Enums.MediaStatus.Deleted;
        deleted.ExpiresAt = DateTimeOffset.UtcNow.AddDays(3);

        Assert.True((await Build("https://m.example.com", live).GetByIdAsync(Guid.NewGuid())).IsAvailable);
        Assert.False((await Build("https://m.example.com", expired).GetByIdAsync(Guid.NewGuid())).IsAvailable);
        Assert.False((await Build("https://m.example.com", deleted).GetByIdAsync(Guid.NewGuid())).IsAvailable);
    }

    [Fact]
    public async Task A_trailing_slash_on_the_base_url_does_not_double_up()
    {
        var service = Build("https://media.example.com/", StoredWithLocalhostUrl());

        var dto = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Equal("https://media.example.com/media/invoice.pdf", dto.PublicUrl);
    }

    [Fact]
    public async Task The_paged_list_uses_the_current_base_url_too()
    {
        var service = Build("http://203.0.113.7:18080", StoredWithLocalhostUrl());

        var page = await service.GetPagedAsync(1, 10, null);

        Assert.Equal("http://203.0.113.7:18080/media/invoice.pdf", page.Items.Single().PublicUrl);
    }
}
