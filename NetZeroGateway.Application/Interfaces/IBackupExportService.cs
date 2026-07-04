namespace NetZeroGateway.Application.Interfaces;

public interface IBackupExportService
{
    /// <summary>
    /// Builds an xlsx workbook: log tables filtered to [from, to), configuration
    /// tables always in full. Null bounds mean unbounded (all time).
    /// </summary>
    Task<byte[]> ExportXlsxAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);
}
