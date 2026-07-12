namespace NetZeroGateway.Application.Interfaces;

/// <summary>Physical file storage — implemented in Infrastructure (local disk).</summary>
public interface IMediaStorageService
{
    Task SaveAsync(string storedFileName, Stream content, CancellationToken ct = default);
    Stream? OpenRead(string storedFileName);
    void Delete(string storedFileName);
}
