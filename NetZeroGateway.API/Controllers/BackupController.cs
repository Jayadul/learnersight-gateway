using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.Interfaces;

namespace NetZeroGateway.API.Controllers;

[ApiController]
[Route("api/backup")]
public class BackupController(IBackupExportService exportService) : ControllerBase
{
    private const string XlsxMime =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Download an xlsx snapshot; omit both dates for all time.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var bytes = await exportService.ExportXlsxAsync(from, to, ct);
        var fileName = $"netzero-backup-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.xlsx";
        return File(bytes, XlsxMime, fileName);
    }
}
