using System.Linq.Expressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// Hotfix batch 6: every sync covers a complete business-day (00:00:00 to 23:59:59), rows still
/// waiting from earlier days are asked about again, and a row with no clear answer after a day
/// becomes Unknown instead of staying In Progress forever.
/// </summary>
public class StatusSyncWindowRulesTests
{
    private static readonly Guid ConfigId = Guid.NewGuid();
    private static readonly TimeZoneInfo Dhaka = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");

    private sealed class Repo(List<DispatchJobRecipient> rows) : IDispatchJobRecipientRepository
    {
        public DateTimeOffset? From { get; private set; }
        public DateTimeOffset? To { get; private set; }
        public Task<List<DispatchJobRecipient>> GetPendingForChannelAsync(Guid id, DateTimeOffset from, DateTimeOffset to, int limit)
        { From = from; To = to; return Task.FromResult(rows); }
        public Task<List<DispatchJobRecipient>> GetByDispatchJobIdAsync(Guid id) => throw new NotSupportedException();
        public Task<(List<DispatchJobRecipient> Items, int Total)> GetPagedAsync(
            int page, int size, DateTimeOffset? from, DateTimeOffset? to, ChannelType? channelType,
            Guid? channelConfigId, DeliveryStatus? deliveryStatus, string? recipient, string? failureReason = null)
            => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<DispatchJobRecipient> r) => throw new NotSupportedException();
        public Task<DispatchJobRecipient?> GetByIdAsync(Guid id) => throw new NotSupportedException();
        public Task<List<DispatchJobRecipient>> GetAllAsync() => throw new NotSupportedException();
        public Task<List<DispatchJobRecipient>> FindAsync(Expression<Func<DispatchJobRecipient, bool>> p) => throw new NotSupportedException();
        public Task AddAsync(DispatchJobRecipient e) => throw new NotSupportedException();
        public void Update(DispatchJobRecipient e) { }
        public void Delete(DispatchJobRecipient e) => throw new NotSupportedException();
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
    }

    private sealed class ConfigRepo : IChannelConfigRepository
    {
        private static ChannelConfig C() => new()
        {
            Id = ConfigId, ChannelType = ChannelType.SMS, Provider = Provider.Reve, Label = "SMS",
            LabelSlug = "sms", IsActive = true, ConfigJson = "{}"
        };
        public Task<ChannelConfig?> GetByIdAsync(Guid id) => Task.FromResult<ChannelConfig?>(C());
        public Task<List<ChannelConfig>> GetAllAsync() => Task.FromResult(new List<ChannelConfig> { C() });
        public Task<ChannelConfig?> GetDefaultByChannelTypeAsync(ChannelType type) => throw new NotSupportedException();
        public Task<ChannelConfig?> GetActiveBySlugAsync(ChannelType type, string labelSlug) => throw new NotSupportedException();
        public Task<List<ChannelConfig>> GetAllByChannelTypeAsync(ChannelType type) => throw new NotSupportedException();
        public Task SetDefaultAsync(Guid id, ChannelType type) => throw new NotSupportedException();
        public Task<List<ChannelConfig>> FindAsync(Expression<Func<ChannelConfig, bool>> p) => throw new NotSupportedException();
        public Task AddAsync(ChannelConfig e) => throw new NotSupportedException();
        public void Update(ChannelConfig e) { }
        public void Delete(ChannelConfig e) => throw new NotSupportedException();
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
    }

    private sealed class Provider_(DateTimeOffset[]? sawFrom = null) : IChannelStatusSyncProvider
    {
        public DateTimeOffset? SawFrom { get; private set; }
        public Provider Provider => Provider.Reve;
        public Task<StatusSyncResult> SyncAsync(Dictionary<string, string> cfg, List<DispatchJobRecipient> rows,
            DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
        { SawFrom = from; return Task.FromResult(StatusSyncResult.Ok(0, rows.Count)); }
    }

    private static (StatusSyncService Service, Repo Repo, Provider_ Provider) Build(List<DispatchJobRecipient>? rows = null)
    {
        var repo = new Repo(rows ?? []);
        var provider = new Provider_();
        var service = new StatusSyncService(new ConfigRepo(), repo, new PlainTextEncryption(), [provider],
            new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["App:TimeZone"] = "Asia/Dhaka" }).Build(),
            NullLogger<StatusSyncService>.Instance);
        return (service, repo, provider);
    }

    private static DispatchJobRecipient Pending(DateTimeOffset sentAt) => new()
    {
        Id = Guid.NewGuid(), Recipient = "8801748848039", DeliveryStatus = DeliveryStatus.Pending,
        CreatedAt = sentAt, DispatchJob = new DispatchJob { SentAt = sentAt }
    };

    private static (DateTimeOffset Start, DateTimeOffset End) TodayInDhaka()
    {
        var start = NetZeroGateway.Application.Utilities.AppTime.StartOfDayUtc(DateTimeOffset.UtcNow, Dhaka);
        return (start, start.AddDays(1).AddMilliseconds(-1));
    }

    [Fact]
    public async Task Sync_today_covers_the_whole_business_day_and_looks_back_for_waiting_rows()
    {
        var (service, repo, _) = Build();
        var (start, end) = TodayInDhaka();

        var result = await service.SyncChannelAsync(ConfigId, new StatusSyncRequest());

        Assert.Equal(start, result.From);
        Assert.Equal(end, result.To);
        Assert.Equal(start.AddDays(-30), repo.From);
        Assert.Equal(end, repo.To);
    }

    [Fact]
    public async Task The_window_runs_from_midnight_to_the_last_second_of_the_day()
    {
        var (service, _, _) = Build();

        var result = await service.SyncChannelAsync(ConfigId, new StatusSyncRequest());

        var from = TimeZoneInfo.ConvertTime(result.From, Dhaka);
        var to = TimeZoneInfo.ConvertTime(result.To, Dhaka);
        Assert.Equal(TimeSpan.Zero, from.TimeOfDay);
        Assert.Equal(new TimeSpan(0, 23, 59, 59, 999), to.TimeOfDay);
        Assert.Equal(from.Date, to.Date);
    }

    [Fact]
    public async Task A_custom_range_is_snapped_to_whole_days_and_does_not_look_back()
    {
        var (service, repo, _) = Build();
        var mid = new DateTimeOffset(2026, 9, 10, 14, 30, 0, TimeSpan.FromHours(6));

        var result = await service.SyncChannelAsync(ConfigId, new StatusSyncRequest { From = mid, To = mid.AddDays(2) });

        Assert.Equal(new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.FromHours(6)), result.From);
        Assert.Equal(new DateTimeOffset(2026, 9, 12, 23, 59, 59, 999, TimeSpan.FromHours(6)), result.To);
        Assert.Equal(result.From, repo.From);
    }

    [Fact]
    public async Task The_nightly_run_covers_yesterday_and_looks_back_for_missed_nights()
    {
        var (service, repo, _) = Build();
        var (todayStart, _) = TodayInDhaka();
        var yesterdayStart = todayStart.AddDays(-1);

        await service.SyncAllAsync();

        Assert.Equal(yesterdayStart.AddDays(-30), repo.From);
        Assert.Equal(todayStart.AddMilliseconds(-1), repo.To);
    }

    [Fact]
    public async Task A_provider_reading_a_report_window_is_given_the_oldest_waiting_row()
    {
        var old = DateTimeOffset.UtcNow.AddDays(-4);
        var (service, _, provider) = Build([Pending(old)]);

        await service.SyncChannelAsync(ConfigId, new StatusSyncRequest());

        Assert.Equal(old, provider.SawFrom);
    }

    [Fact]
    public async Task A_row_with_no_clear_answer_after_a_day_becomes_unknown()
    {
        var stale = Pending(DateTimeOffset.UtcNow.AddHours(-30));
        var (service, _, _) = Build([stale]);

        await service.SyncChannelAsync(ConfigId, new StatusSyncRequest());

        Assert.Equal(DeliveryStatus.Unknown, stale.DeliveryStatus);
    }

    [Fact]
    public async Task A_recent_row_is_left_in_progress()
    {
        var recent = Pending(DateTimeOffset.UtcNow.AddHours(-2));
        var (service, _, _) = Build([recent]);

        await service.SyncChannelAsync(ConfigId, new StatusSyncRequest());

        Assert.Equal(DeliveryStatus.Pending, recent.DeliveryStatus);
    }
}
