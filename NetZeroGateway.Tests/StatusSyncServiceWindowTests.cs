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
/// Found while auditing Seq for hotfix batch 3: every status-sync run, nightly and on
/// demand, for every provider, was throwing before it read a single row.
/// AppTime.ToAppTime is a display conversion — it returns the business-zone offset
/// (+06:00), not UTC — and that value was flowing straight into a query against
/// dispatch_job_recipients' UTC timestamptz columns. Npgsql refuses to write a
/// non-zero-offset DateTimeOffset there at all. These pin the fix: the window handed to
/// the repository is always UTC-normalized, on both the default-window and the
/// explicit-window path.
/// </summary>
public class StatusSyncServiceWindowTests
{
    private static readonly Guid ConfigId = Guid.NewGuid();

    private sealed class CapturingRecipientRepo : IDispatchJobRecipientRepository
    {
        public DateTimeOffset? CapturedFrom { get; private set; }
        public DateTimeOffset? CapturedTo { get; private set; }

        public Task<List<DispatchJobRecipient>> GetPendingForChannelAsync(
            Guid channelConfigId, DateTimeOffset from, DateTimeOffset to, int limit)
        {
            CapturedFrom = from;
            CapturedTo = to;
            return Task.FromResult(new List<DispatchJobRecipient>());
        }

        public Task<List<DispatchJobRecipient>> GetByDispatchJobIdAsync(Guid id) => Unused.Fail<Task<List<DispatchJobRecipient>>>();
        public Task<(List<DispatchJobRecipient> Items, int Total)> GetPagedAsync(
            int page, int size, DateTimeOffset? from, DateTimeOffset? to,
            ChannelType? channelType, Guid? channelConfigId,
            DeliveryStatus? deliveryStatus, string? recipient, string? failureReason = null)
            => Unused.Fail<Task<(List<DispatchJobRecipient>, int)>>();
        public Task AddRangeAsync(IEnumerable<DispatchJobRecipient> recipients) => Unused.Fail<Task>();
        public Task<DispatchJobRecipient?> GetByIdAsync(Guid id) => Unused.Fail<Task<DispatchJobRecipient?>>();
        public Task<List<DispatchJobRecipient>> GetAllAsync() => Unused.Fail<Task<List<DispatchJobRecipient>>>();
        public Task<List<DispatchJobRecipient>> FindAsync(Expression<Func<DispatchJobRecipient, bool>> p) => Unused.Fail<Task<List<DispatchJobRecipient>>>();
        public Task AddAsync(DispatchJobRecipient entity) => Unused.Fail<Task>();
        public void Update(DispatchJobRecipient entity) { }
        public void Delete(DispatchJobRecipient entity) => Unused.Fail<bool>();
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
    }

    private sealed class SingleConfigRepo(ChannelConfig config) : IChannelConfigRepository
    {
        public Task<ChannelConfig?> GetByIdAsync(Guid id) => Task.FromResult<ChannelConfig?>(config);
        public Task<List<ChannelConfig>> GetAllAsync() => Task.FromResult(new List<ChannelConfig> { config });
        public Task<ChannelConfig?> GetDefaultByChannelTypeAsync(ChannelType type) => Unused.Fail<Task<ChannelConfig?>>();
        public Task<ChannelConfig?> GetActiveBySlugAsync(ChannelType type, string labelSlug) => Unused.Fail<Task<ChannelConfig?>>();
        public Task<List<ChannelConfig>> GetAllByChannelTypeAsync(ChannelType type) => Unused.Fail<Task<List<ChannelConfig>>>();
        public Task SetDefaultAsync(Guid id, ChannelType type) => Unused.Fail<Task>();
        public Task<List<ChannelConfig>> FindAsync(Expression<Func<ChannelConfig, bool>> p) => Unused.Fail<Task<List<ChannelConfig>>>();
        public Task AddAsync(ChannelConfig entity) => Unused.Fail<Task>();
        public void Update(ChannelConfig entity) => Unused.Fail<bool>();
        public void Delete(ChannelConfig entity) => Unused.Fail<bool>();
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
    }

    private sealed class NoOpStatusSyncProvider : IChannelStatusSyncProvider
    {
        public Provider Provider => Provider.Reve;
        public Task<StatusSyncResult> SyncAsync(
            Dictionary<string, string> config, List<DispatchJobRecipient> rows,
            DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
            => Task.FromResult(StatusSyncResult.Ok(0, 0));
    }

    private static ChannelConfig Config() => new()
    {
        Id = ConfigId,
        ChannelType = ChannelType.SMS,
        Provider = Provider.Reve,
        ProviderName = "REVE",
        Label = "SMS Default",
        LabelSlug = "smsdefault",
        IsActive = true,
        ConfigJson = "{}",
    };

    private static (StatusSyncService Service, CapturingRecipientRepo Recipients) Build()
    {
        var recipients = new CapturingRecipientRepo();
        var service = new StatusSyncService(
            new SingleConfigRepo(Config()),
            recipients,
            new PlainTextEncryption(),
            [new NoOpStatusSyncProvider()],
            new ConfigurationBuilder().Build(),
            NullLogger<StatusSyncService>.Instance);
        return (service, recipients);
    }

    [Fact]
    public async Task The_default_window_is_UTC_normalized()
    {
        var (service, recipients) = Build();

        await service.SyncAllAsync();

        Assert.Equal(TimeSpan.Zero, recipients.CapturedFrom!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, recipients.CapturedTo!.Value.Offset);
    }

    [Fact]
    public async Task An_explicit_window_is_also_UTC_normalized()
    {
        var (service, recipients) = Build();
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");
        var dhakaNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);

        await service.SyncChannelAsync(ConfigId, new StatusSyncRequest
        {
            From = dhakaNow.AddDays(-1),
            To = dhakaNow,
        });

        Assert.Equal(TimeSpan.Zero, recipients.CapturedFrom!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, recipients.CapturedTo!.Value.Offset);
    }
}
