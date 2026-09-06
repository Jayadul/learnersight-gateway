using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// Balances are cached on the channel row and refreshed on a schedule. The behaviour
/// worth pinning is what happens when a provider is unreachable: blanking the figure
/// would read as "no credit left" on the dashboard, which is a different and alarming
/// claim from "we could not check just now".
/// </summary>
public class ChannelBalanceServiceTests
{
    private sealed class StubBalanceProvider(
        Provider provider, ChannelBalanceResult result) : IChannelBalanceProvider
    {
        public Provider Provider { get; } = provider;
        public Task<ChannelBalanceResult> GetBalanceAsync(
            Dictionary<string, string> config, CancellationToken ct = default)
            => Task.FromResult(result);
    }

    private sealed class FakeConfigRepo(params ChannelConfig[] configs) : IChannelConfigRepository
    {
        public List<ChannelConfig> Configs { get; } = [.. configs];

        public Task<List<ChannelConfig>> GetAllAsync() => Task.FromResult(Configs);
        public Task<ChannelConfig?> GetByIdAsync(Guid id)
            => Task.FromResult(Configs.FirstOrDefault(c => c.Id == id));
        public void Update(ChannelConfig entity) { }
        public Task<int> SaveChangesAsync() => Task.FromResult(0);

        public Task<ChannelConfig?> GetDefaultByChannelTypeAsync(ChannelType type)
            => Unused.Fail<Task<ChannelConfig?>>();
        public Task<ChannelConfig?> GetActiveBySlugAsync(ChannelType type, string labelSlug)
            => Unused.Fail<Task<ChannelConfig?>>();
        public Task<List<ChannelConfig>> GetAllByChannelTypeAsync(ChannelType type)
            => Unused.Fail<Task<List<ChannelConfig>>>();
        public Task SetDefaultAsync(Guid id, ChannelType type) => Unused.Fail<Task>();
        public Task<bool> SlugExistsAsync(ChannelType type, string slug, Guid? excludeId)
            => Unused.Fail<Task<bool>>();
        public Task AddAsync(ChannelConfig entity) => Unused.Fail<Task>();
        public Task<List<ChannelConfig>> FindAsync(Expression<Func<ChannelConfig, bool>> p)
            => Unused.Fail<Task<List<ChannelConfig>>>();
        public void Delete(ChannelConfig entity) => Unused.Fail<bool>();
    }

    private static ChannelConfig SmsChannel(decimal? existingBalance = null) => new()
    {
        Id = Guid.NewGuid(),
        ChannelType = ChannelType.SMS,
        Provider = Provider.Reve,
        ProviderName = "REVE",
        Label = "SMS Default",
        IsActive = true,
        ConfigJson = "{}",
        Balance = existingBalance,
        BalanceCheckedAt = existingBalance is null ? null : DateTimeOffset.UtcNow.AddHours(-1)
    };

    private static ChannelBalanceService Build(
        FakeConfigRepo repo, ChannelBalanceResult result) =>
        new(repo, new PlainTextEncryption(),
            [new StubBalanceProvider(Provider.Reve, result)],
            NullLogger<ChannelBalanceService>.Instance);

    [Fact]
    public async Task A_successful_refresh_records_the_figure_and_clears_any_error()
    {
        var config = SmsChannel();
        var repo = new FakeConfigRepo(config);
        var service = Build(repo, ChannelBalanceResult.Ok(1234.50m, creditLimit: 5000m));

        var dto = await service.RefreshAsync(config.Id);

        Assert.Equal(1234.50m, dto.Available);
        Assert.Equal(5000m, dto.CreditLimit);
        Assert.Null(dto.Error);
        Assert.NotNull(dto.CheckedAt);
    }

    [Fact]
    public async Task A_failed_refresh_keeps_the_last_known_figure_and_marks_it_stale()
    {
        var config = SmsChannel(existingBalance: 900m);
        var previouslyCheckedAt = config.BalanceCheckedAt;
        var repo = new FakeConfigRepo(config);
        var service = Build(repo, ChannelBalanceResult.Failed("session expired"));

        var dto = await service.RefreshAsync(config.Id);

        // The figure survives, its timestamp is untouched so its age is honest, and the
        // card can say why it is stale rather than showing an empty balance.
        Assert.Equal(900m, dto.Available);
        Assert.Equal(previouslyCheckedAt, dto.CheckedAt);
        Assert.Equal("session expired", dto.Error);
        Assert.True(dto.IsStale);
    }

    [Fact]
    public async Task A_channel_that_never_checked_successfully_is_not_stale_it_is_empty()
    {
        // Nothing to show is not the same as a stale figure, and the UI treats them
        // differently — one says "not checked yet", the other shows a number with a mark.
        var config = SmsChannel();
        var repo = new FakeConfigRepo(config);
        var service = Build(repo, ChannelBalanceResult.Failed("unreachable"));

        var dto = await service.RefreshAsync(config.Id);

        Assert.Null(dto.Available);
        Assert.False(dto.IsStale);
        Assert.Equal("unreachable", dto.Error);
    }

    [Fact]
    public async Task Channels_whose_provider_reports_no_balance_are_left_out_entirely()
    {
        // Voice and WhatsApp have no balance endpoint. Listing them with a blank figure
        // would read as zero credit rather than "not applicable".
        var voice = SmsChannel();
        voice.ChannelType = ChannelType.Voice;
        voice.Provider = Provider.NetZeroVoice;

        var repo = new FakeConfigRepo(SmsChannel(), voice);
        var service = Build(repo, ChannelBalanceResult.Ok(10m));

        var all = await service.GetAllAsync();

        Assert.Single(all);
        Assert.Equal(Provider.Reve, all[0].Provider);
    }

    [Fact]
    public async Task Refreshing_a_provider_without_a_balance_reader_is_refused_clearly()
    {
        var voice = SmsChannel();
        voice.Provider = Provider.NetZeroVoice;
        var repo = new FakeConfigRepo(voice);
        var service = Build(repo, ChannelBalanceResult.Ok(10m));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RefreshAsync(voice.Id));

        Assert.Contains("does not report a balance", ex.Message);
    }
}
