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
    public async Task Sync_now_asks_about_every_waiting_row_whenever_it_was_sent()
    {
        var (service, repo, _) = Build();

        await service.SyncChannelAsync(ConfigId, new StatusSyncRequest());

        // No lower bound at all: a row from months ago is inside the query.
        Assert.True(repo.From <= new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.True(repo.To >= DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, repo.From!.Value.Offset);
    }

    [Fact]
    public async Task A_row_from_two_months_ago_is_handed_to_the_provider_and_resolved()
    {
        var old = Pending(DateTimeOffset.UtcNow.AddDays(-60));
        var (service, _, provider) = Build([old]);

        var result = await service.SyncChannelAsync(ConfigId, new StatusSyncRequest());

        Assert.Equal(old.CreatedAt, provider.SawFrom);
        // Sixty days with no clear answer: resolved to Unknown, no longer In Progress.
        Assert.Equal(DeliveryStatus.Unknown, old.DeliveryStatus);
        Assert.Equal(1, result.Unmatched);
    }

    [Fact]
    public async Task The_result_says_when_more_rows_are_waiting_than_one_run_takes()
    {
        var rows = Enumerable.Range(0, 3).Select(_ => Pending(DateTimeOffset.UtcNow.AddHours(-1))).ToList();
        var (service, _, _) = Build(rows);

        var partial = await service.SyncChannelAsync(ConfigId, new StatusSyncRequest { Limit = 3 });
        var complete = await service.SyncChannelAsync(ConfigId, new StatusSyncRequest { Limit = 10 });

        Assert.True(partial.MoreWaiting);
        Assert.False(complete.MoreWaiting);
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
    public async Task The_nightly_run_also_asks_about_every_waiting_row()
    {
        var (service, repo, _) = Build();

        await service.SyncAllAsync();

        Assert.True(repo.From <= new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));
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
