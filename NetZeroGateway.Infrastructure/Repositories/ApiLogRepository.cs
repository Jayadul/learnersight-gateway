using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class ApiLogRepository(NetZeroGatewayDbContext db)
    : Repository<ApiLog>(db), IApiLogRepository
{
    public async Task<(List<ApiLog> Items, int Total)> GetPagedAsync(
        int page, int size,
        DateTimeOffset? from, DateTimeOffset? to,
        int? responseStatus, string? channel, string? callerIp = null, string? number = null,
        string? gatewayResult = null)
    {
        var q = _set.AsQueryable();
        if (from is not null) q = q.Where(l => l.RequestedAt >= from);
        if (to is not null) q = q.Where(l => l.RequestedAt <= to);
        if (responseStatus is not null) q = q.Where(l => l.ResponseStatus == responseStatus);
        // Contains, not equals: the snapshot lists every channel a request asked for, so
        // filtering on WhatsApp must also match "SMS, WhatsApp".
        if (!string.IsNullOrEmpty(channel))
            q = q.Where(l => l.RequestedChannels != null && l.RequestedChannels.Contains(channel));
        // The caller's IP address (the "Caller IP" column). Contains, so a partial address such as
        // "192.168" finds the whole range; the filter box existed but the backend never received or
        // applied it (hotfix batch 8). This is NOT the callerID an integrator sends in the request.
        if (!string.IsNullOrWhiteSpace(callerIp))
        {
            var ip = callerIp.Trim();
            q = q.Where(l => l.CallerIp.Contains(ip));
        }
        // ToUser is comma-joined for a batched send (hotfix batch 9, Task 7) — substring
        // match so searching one number finds it inside "8801..,8801..,8801.." too, not
        // only a row where it was the sole recipient.
        if (!string.IsNullOrWhiteSpace(number))
        {
            var n = number.Trim();
            q = q.Where(l => l.ToUser != null && l.ToUser.Contains(n));
        }
        // The gateway-level outcome (hotfix batch 9, Task 12) — every /sendtext call
        // returns HTTP 200 regardless, so this is the only way to actually find a
        // rejected request; responseStatus above cannot distinguish them.
        if (!string.IsNullOrWhiteSpace(gatewayResult))
            q = q.Where(l => l.GatewayResult == gatewayResult);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(l => l.RequestedAt)
            .Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total);
    }
}
