using System.Linq.Expressions;
using Microsoft.Extensions.Configuration;
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
            new ConfigurationBuilder().Build(),
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
    public async Task The_public_balance_endpoint_reports_lastCheckedAt_in_Dhaka_time()
    {
        // Hotfix batch 9, Task 3: GET /balance is read directly by third-party
        // integrators, with no Angular date pipe to convert it — the service itself
        // must hand back the business display zone (+06:00), not raw UTC.
        var config = SmsChannel();
        config.LabelSlug = "smsdefault";
        var repo = new FakeConfigRepo(config);
        var service = Build(repo, ChannelBalanceResult.Ok(500m));

        var dto = await service.GetByLabelAsync("smsdefault");

        Assert.NotNull(dto);
        Assert.NotNull(dto!.LastCheckedAt);
        Assert.Equal(TimeSpan.FromHours(6), dto.LastCheckedAt!.Value.Offset);
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

    [Fact]
    public async Task The_public_balance_lookup_resolves_an_active_balance_capable_channel_by_label()
    {
        var config = SmsChannel(existingBalance: 250m);
        config.LabelSlug = "revemain";
        config.Label = "SMS Default";
        var repo = new FakeConfigRepo(config);
        var service = Build(repo, ChannelBalanceResult.Ok(250m));

        var dto = await service.GetByLabelAsync("REVEMAIN"); // case-insensitive, like a gateway tag

        Assert.NotNull(dto);
        Assert.Equal("SMS Default", dto!.Label);
        Assert.Equal(250m, dto.Balance);
    }

    [Fact]
    public async Task The_public_balance_lookup_calls_the_provider_live_rather_than_the_cache()
    {
        // A stale cached figure that must NOT be what comes back — proves this reads the
        // provider on every call rather than the cache RefreshAsync/GetAllAsync use.
        var config = SmsChannel(existingBalance: 100m);
        config.LabelSlug = "revemain";
        var repo = new FakeConfigRepo(config);
        var service = Build(repo, ChannelBalanceResult.Ok(777m));

        var dto = await service.GetByLabelAsync("revemain");

        Assert.Equal(777m, dto!.Balance);
        Assert.NotEqual(100m, dto.Balance);
    }

    [Fact]
    public async Task The_public_balance_lookup_falls_back_to_the_last_known_figure_when_the_live_call_fails()
    {
        // Same graceful-degradation rule as every other refresh path: a transient
        // provider failure must not turn into a bare error for an integrator polling
        // this endpoint.
        var config = SmsChannel(existingBalance: 900m);
        config.LabelSlug = "revemain";
        var repo = new FakeConfigRepo(config);
        var service = Build(repo, ChannelBalanceResult.Failed("session expired"));

        var dto = await service.GetByLabelAsync("revemain");

        Assert.NotNull(dto);
        Assert.Equal(900m, dto!.Balance);
    }

    [Fact]
    public async Task The_public_balance_lookup_returns_null_for_an_unknown_label()
    {
        var config = SmsChannel();
        config.LabelSlug = "revemain";
        var repo = new FakeConfigRepo(config);
        var service = Build(repo, ChannelBalanceResult.Ok(10m));

        Assert.Null(await service.GetByLabelAsync("nosuchlabel"));
    }

    [Fact]
    public async Task The_public_balance_lookup_returns_null_for_an_inactive_channel()
    {
        // Never leaks whether an inactive channel's label exists — same null, same
        // generic error, as a label that was never valid at all.
        var config = SmsChannel();
        config.LabelSlug = "revemain";
        config.IsActive = false;
        var repo = new FakeConfigRepo(config);
        var service = Build(repo, ChannelBalanceResult.Ok(10m));

        Assert.Null(await service.GetByLabelAsync("revemain"));
    }

    [Fact]
    public async Task The_public_balance_lookup_returns_null_for_a_non_balance_provider()
    {
        var voice = SmsChannel();
        voice.LabelSlug = "voicemain";
        voice.ChannelType = ChannelType.Voice;
        voice.Provider = Provider.NetZeroVoice;
        var repo = new FakeConfigRepo(voice);
        var service = Build(repo, ChannelBalanceResult.Ok(10m));

        Assert.Null(await service.GetByLabelAsync("voicemain"));
    }
}
