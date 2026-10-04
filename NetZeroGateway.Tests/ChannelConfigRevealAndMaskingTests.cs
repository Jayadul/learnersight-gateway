using System.Linq.Expressions;
using System.Text.Json;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// Hotfix batch 3, task 6: channel secrets show a partial mask (last few characters) with
/// a reveal-on-click action for the real value, replacing the old all-or-nothing "***".
/// The part worth pinning is that the round-trip protection still works — saving a config
/// back with the masked value untouched must not overwrite the real secret with the mask.
/// </summary>
public class ChannelConfigRevealAndMaskingTests
{
    private const string RealApiKey = "nzg_i5uuzmgy9l3u7ddnaonaw98zvsz2";

    private sealed class NoOpGatewayCredentialService : IGatewayCredentialService
    {
        public Task<GatewayCredentialDto> GetAsync() => Unused.Fail<Task<GatewayCredentialDto>>();
        public Task<GatewayCredentialDto> UpdateAsync(UpdateGatewayCredentialRequest request)
            => Unused.Fail<Task<GatewayCredentialDto>>();
        public Task SyncUsernameFromDefaultSmsChannelAsync(string? balanceClientId) => Task.CompletedTask;
    }

    /// <summary>
    /// A mutable single-row repository — the shared FakeChannelConfigRepository in
    /// Fakes.cs is built for dispatch-resolution tests and deliberately throws on
    /// Update/GetAllByChannelTypeAsync, which UpdateAsync (via ResolveSlugAsync) needs.
    /// </summary>
    private sealed class MutableChannelConfigRepository(ChannelConfig stored) : IChannelConfigRepository
    {
        public Task<ChannelConfig?> GetByIdAsync(Guid id) => Task.FromResult<ChannelConfig?>(stored);
        public Task<ChannelConfig?> GetDefaultByChannelTypeAsync(ChannelType type) => Unused.Fail<Task<ChannelConfig?>>();
        public Task<ChannelConfig?> GetActiveBySlugAsync(ChannelType type, string labelSlug) => Unused.Fail<Task<ChannelConfig?>>();
        public Task<List<ChannelConfig>> GetAllByChannelTypeAsync(ChannelType type) => Task.FromResult(new List<ChannelConfig> { stored });
        public Task SetDefaultAsync(Guid id, ChannelType type) => Unused.Fail<Task>();
        public Task<List<ChannelConfig>> GetAllAsync() => Unused.Fail<Task<List<ChannelConfig>>>();
        public Task<List<ChannelConfig>> FindAsync(Expression<Func<ChannelConfig, bool>> p) => Unused.Fail<Task<List<ChannelConfig>>>();
        public Task AddAsync(ChannelConfig entity) => Unused.Fail<Task>();
        public void Update(ChannelConfig entity) { }
        public void Delete(ChannelConfig entity) => Unused.Fail<bool>();
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
    }

    private static ChannelConfig Config() => new()
    {
        Id = Guid.NewGuid(),
        ChannelType = ChannelType.SMS,
        Provider = Provider.Reve,
        ProviderName = "REVE",
        Label = "SMS Default",
        LabelSlug = "smsdefault",
        IsActive = true,
        ConfigJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["apikey"] = RealApiKey,
            ["baseUrl"] = "https://smpp.example.com",
        }),
    };

    private static ChannelConfigService Build(ChannelConfig config) => new(
        new MutableChannelConfigRepository(config),
        new FakeFallbackPolicyRepository(),
        new FakeChannelTemplateRepository(),
        new FakeDispatchJobRepository(),
        new PlainTextEncryption(),
        new NoOpGatewayCredentialService());

    [Fact]
    public async Task Reads_show_a_partial_mask_not_the_full_secret()
    {
        var service = Build(Config());

        var dto = await service.GetByIdAsync(Guid.NewGuid()); // fake repo ignores the id

        var masked = dto.ConfigFields["apikey"];
        Assert.NotEqual(RealApiKey, masked);
        Assert.EndsWith("zvsz2", masked);
        Assert.DoesNotContain("nzg_i5uuzmgy9l3u7ddnaonaw98", masked);
        // Non-secret fields are untouched.
        Assert.Equal("https://smpp.example.com", dto.ConfigFields["baseUrl"]);
    }

    [Fact]
    public async Task Reveal_returns_the_real_value()
    {
        var service = Build(Config());

        var value = await service.RevealFieldAsync(Guid.NewGuid(), "apikey");

        Assert.Equal(RealApiKey, value);
    }

    [Fact]
    public async Task Saving_back_the_partial_mask_keeps_the_stored_secret()
    {
        var config = Config();
        var service = Build(config);
        var maskedApiKey = (await service.GetByIdAsync(config.Id)).ConfigFields["apikey"];

        // Exactly what a save-without-touching-the-field sends: the masked value the UI
        // was shown, unmodified.
        var updateJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["apikey"] = maskedApiKey,
            ["baseUrl"] = "https://smpp.example.com",
        });

        await service.UpdateAsync(config.Id, new UpdateChannelConfigRequest
        {
            Provider = Provider.Reve,
            ProviderName = "REVE",
            Label = "SMS Default",
            LabelSlug = "smsdefault",
            DelaySeconds = 0,
            ConfigJson = updateJson,
        });

        Assert.Equal(RealApiKey, await service.RevealFieldAsync(config.Id, "apikey"));
    }
}
