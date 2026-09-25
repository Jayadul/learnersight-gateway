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
/// Hotfix batch 7 follow-up: the dialog said "Resolved 27 recipient(s)" while all 27 stayed In
/// Progress, because rows the provider merely reported as still in flight were counted as resolved.
/// The counts now come from what actually happened to each row.
/// </summary>
public class StatusSyncCountsTests
{
    private static readonly Guid ConfigId = Guid.NewGuid();

    private sealed class Repo(List<DispatchJobRecipient> rows) : IDispatchJobRecipientRepository
    {
        public Task<List<DispatchJobRecipient>> GetPendingForChannelAsync(Guid id, DateTimeOffset from, DateTimeOffset to, int limit)
            => Task.FromResult(rows);
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

    /// <summary>Applies the given answer to every row and claims it "updated" them all, like a real provider does.</summary>
    private sealed class AnsweringProvider(DeliveryStatus answer) : IChannelStatusSyncProvider
    {
        public Provider Provider => Provider.Reve;
        public Task<StatusSyncResult> SyncAsync(Dictionary<string, string> cfg, List<DispatchJobRecipient> rows,
            DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
        {
            foreach (var r in rows) r.DeliveryStatus = answer;
            return Task.FromResult(StatusSyncResult.Ok(rows.Count, 0));
        }
    }

    private static DispatchJobRecipient Row(DeliveryStatus status, double hoursAgo) => new()
    {
        Id = Guid.NewGuid(), Recipient = "8801748848039", DeliveryStatus = status,
        CreatedAt = DateTimeOffset.UtcNow.AddHours(-hoursAgo),
        DispatchJob = new DispatchJob { SentAt = DateTimeOffset.UtcNow.AddHours(-hoursAgo) }
    };

    private static Task<StatusSyncResultDto> Run(DeliveryStatus providerAnswer, params DispatchJobRecipient[] rows)
        => new StatusSyncService(new ConfigRepo(), new Repo([.. rows]), new PlainTextEncryption(),
                [new AnsweringProvider(providerAnswer)],
                new ConfigurationBuilder().AddInMemoryCollection(
                    new Dictionary<string, string?> { ["App:TimeZone"] = "Asia/Dhaka" }).Build(),
                NullLogger<StatusSyncService>.Instance)
            .SyncChannelAsync(ConfigId, new StatusSyncRequest());

    [Fact]
    public async Task Rows_the_provider_says_are_still_in_flight_are_not_counted_as_resolved()
    {
        var result = await Run(DeliveryStatus.Pending, Row(DeliveryStatus.Pending, 1), Row(DeliveryStatus.Pending, 2));

        Assert.Equal(0, result.Updated);
        Assert.Equal(2, result.StillWaiting);
    }

    [Fact]
    public async Task Rows_answered_delivered_are_counted_as_resolved()
    {
        var result = await Run(DeliveryStatus.Delivered, Row(DeliveryStatus.Pending, 1), Row(DeliveryStatus.Pending, 2));

        Assert.Equal(2, result.Updated);
        Assert.Equal(0, result.StillWaiting);
    }

    [Fact]
    public async Task A_row_still_in_flight_after_the_grace_period_becomes_unknown_and_is_counted_as_such()
    {
        var old = Row(DeliveryStatus.Pending, 8);
        var recent = Row(DeliveryStatus.Pending, 1);

        var result = await Run(DeliveryStatus.Pending, old, recent);

        Assert.Equal(DeliveryStatus.Unknown, old.DeliveryStatus);
        Assert.Equal(DeliveryStatus.Pending, recent.DeliveryStatus);
        Assert.Equal(1, result.MarkedUnknown);
        Assert.Equal(1, result.StillWaiting);
        Assert.Equal(0, result.Updated);
    }

    [Fact]
    public async Task An_unknown_row_whose_late_delivery_report_arrives_becomes_delivered()
    {
        var unknown = Row(DeliveryStatus.Unknown, 20);

        var result = await Run(DeliveryStatus.Delivered, unknown);

        Assert.Equal(DeliveryStatus.Delivered, unknown.DeliveryStatus);
        Assert.Equal(1, result.Updated);
    }

    [Fact]
    public async Task An_unknown_row_that_stays_unknown_is_not_counted_again()
    {
        var unknown = Row(DeliveryStatus.Unknown, 20);

        var result = await Run(DeliveryStatus.Unknown, unknown);

        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.MarkedUnknown);
    }
}
