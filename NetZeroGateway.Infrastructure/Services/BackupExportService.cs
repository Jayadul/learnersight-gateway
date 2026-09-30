using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Services;

public class BackupExportService(
    NetZeroGatewayDbContext db,
    IConfiguration config
) : IBackupExportService
{
    public async Task<byte[]> ExportXlsxAsync(
        DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var tz = AppTime.Resolve(config);
        var offset = tz.GetUtcOffset(DateTimeOffset.UtcNow);
        DateTime? Local(DateTimeOffset? d) => d is null ? null : AppTime.ToAppTime(d.Value, tz).DateTime;

        // ── Log tables: apply the date range ──
        var msgQuery = db.MessageRequests.AsNoTracking();
        if (from is not null) msgQuery = msgQuery.Where(m => m.ReceivedAt >= from);
        if (to is not null) msgQuery = msgQuery.Where(m => m.ReceivedAt < to);
        var messages = await msgQuery.OrderByDescending(m => m.ReceivedAt).ToListAsync(ct);

        var jobQuery = db.DispatchJobs.AsNoTracking();
        if (from is not null) jobQuery = jobQuery.Where(j => j.CreatedAt >= from);
        if (to is not null) jobQuery = jobQuery.Where(j => j.CreatedAt < to);
        var jobs = await jobQuery.OrderByDescending(j => j.CreatedAt).ToListAsync(ct);

        var apiQuery = db.ApiLogs.AsNoTracking();
        if (from is not null) apiQuery = apiQuery.Where(a => a.RequestedAt >= from);
        if (to is not null) apiQuery = apiQuery.Where(a => a.RequestedAt < to);
        var apiLogs = await apiQuery.OrderByDescending(a => a.RequestedAt).ToListAsync(ct);

        // ── Configuration tables: always in full ──
        var users = await db.AdminUsers.AsNoTracking().ToListAsync(ct);
        var channels = await db.ChannelConfigs.AsNoTracking().ToListAsync(ct);
        var policies = await db.FallbackPolicies.AsNoTracking().ToListAsync(ct);
        var templates = await db.ChannelTemplates.AsNoTracking().ToListAsync(ct);

        using var wb = new XLWorkbook();

        var info = wb.AddWorksheet("Backup Info");
        info.Cell(1, 1).Value = $"Generated at (GMT{offset.Hours:+0;-0})";
        info.Cell(1, 2).Value = DateTimeOffset.UtcNow.ToOffset(offset).DateTime;
        info.Cell(2, 1).Value = "Range from";
        if (from is null) info.Cell(2, 2).Value = "(all time)";
        else info.Cell(2, 2).Value = Local(from)!.Value;
        info.Cell(3, 1).Value = "Range to";
        if (to is null) info.Cell(3, 2).Value = "(now)";
        else info.Cell(3, 2).Value = Local(to)!.Value;
        info.Cell(4, 1).Value = "Note";
        info.Cell(4, 2).Value =
            $"All timestamps in GMT{offset.Hours:+0;-0}. Log sheets are limited to the " +
            "selected range; configuration sheets always contain all rows.";
        info.Column(1).Width = 24;

        AddSheet(wb, "MessageRequests", messages.Select(m => new
        {
            Id = m.Id.ToString(),
            m.CallerID,
            m.ToUser,
            m.RawMessageContent,
            ParsedChannels = string.Join(",", m.ParsedChannels),
            OverallStatus = m.OverallStatus.ToString(),
            ReceivedAt = Local(m.ReceivedAt),
            Source = m.Source.ToString(),
        }));

        AddSheet(wb, "DispatchJobs", jobs.Select(j => new
        {
            Id = j.Id.ToString(),
            MessageRequestId = j.MessageRequestId.ToString(),
            ChannelType = j.ChannelType.ToString(),
            j.ToNumber,
            j.MessageContent,
            j.MediaUrl,
            j.CampaignId,
            Status = j.Status.ToString(),
            j.ProviderMessageId,
            j.ProviderRawResponse,
            j.FailureReason,
            SentAt = Local(j.SentAt),
            j.IsFallback,
            CreatedAt = Local(j.CreatedAt),
        }));

        AddSheet(wb, "ApiLogs", apiLogs.Select(a => new
        {
            Id = a.Id.ToString(),
            a.ToUser,
            a.RawBody,
            a.ResponseBody,
            a.CallerIp,
            a.ResponseStatus,
            a.DurationMs,
            RequestedAt = Local(a.RequestedAt),
            MessageRequestId = a.MessageRequestId?.ToString(),
        }));

        AddSheet(wb, "AdminUsers", users.Select(u => new
        {
            Id = u.Id.ToString(),
            u.Username,
            u.Email,
            Role = u.Role.ToString(),
            u.IsActive,
            LastLoginAt = Local(u.LastLoginAt),
            CreatedAt = Local(u.CreatedAt),
        }));

        AddSheet(wb, "ChannelConfigs", channels.Select(c => new
        {
            Id = c.Id.ToString(),
            ChannelType = c.ChannelType.ToString(),
            c.ProviderName,
            c.Label,
            c.IsActive,
            ConfigJson = c.ConfigJson, // AES-encrypted — safe to export
            CreatedAt = Local(c.CreatedAt),
        }));

        AddSheet(wb, "FallbackPolicies", policies.Select(p => new
        {
            Id = p.Id.ToString(),
            p.Name,
            TriggerChannel = p.TriggerChannel.ToString(),
            FallbackChannel = p.FallbackChannel.ToString(),
            p.IsActive,
            p.IsGlobal,
            p.Description,
            CreatedAt = Local(p.CreatedAt),
        }));

        AddSheet(wb, "ChannelTemplates", templates.Select(t => new
        {
            Id = t.Id.ToString(),
            ChannelConfigId = t.ChannelConfigId.ToString(),
            t.Name,
            VariableLabels = string.Join(",", t.VariableLabels),
            CreatedAt = Local(t.CreatedAt),
        }));

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void AddSheet<T>(XLWorkbook wb, string name, IEnumerable<T> rows)
    {
        var ws = wb.AddWorksheet(name);
        var table = ws.Cell(1, 1).InsertTable(rows, name, createTable: false);
        ws.SheetView.FreezeRows(1);
        table.Row(1).Style.Font.SetBold();
    }
}
