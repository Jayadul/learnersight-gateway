using Microsoft.Extensions.Configuration;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Utilities;

namespace NetZeroGateway.Infrastructure.Services;

/// <summary>Stores media on local disk under Media:StoragePath (a Docker volume in production).</summary>
public class LocalMediaStorageService : IMediaStorageService
{
    private readonly string _root;

    public LocalMediaStorageService(IConfiguration config)
    {
        _root = Path.GetFullPath(config[ConfigKeys.MediaStoragePath] ?? "media");
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(string storedFileName, Stream content, CancellationToken ct = default)
    {
        await using var file = File.Create(ResolvePath(storedFileName));
        await content.CopyToAsync(file, ct);
    }

    public Stream? OpenRead(string storedFileName)
    {
        var path = ResolvePath(storedFileName);
        return File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
    }

    public void Delete(string storedFileName)
    {
        var path = ResolvePath(storedFileName);
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>Rejects path traversal — the resolved file must stay inside the storage root.</summary>
    private string ResolvePath(string storedFileName)
    {
        var path = Path.GetFullPath(Path.Combine(_root, storedFileName));
        if (!path.StartsWith(_root, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid file name.");
        return path;
    }
}
