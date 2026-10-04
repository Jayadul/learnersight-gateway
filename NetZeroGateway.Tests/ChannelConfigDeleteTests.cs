using System.Linq.Expressions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// Hotfix batch 9, Task 16: deleting a channel that has ever sent anything used to hit an
/// unhandled dispatch_jobs FK violation (Restrict, same as FallbackPolicy's) — a raw 500
/// instead of a message the admin can act on. These pin all three block reasons getting
/// the same clean treatment, and that a channel with none of them actually deletes.
/// </summary>
public class ChannelConfigDeleteTests
{
    private sealed class SingleChannelRepository(ChannelConfig channel) : IChannelConfigRepository
    {
        public bool Deleted { get; private set; }
        public Task<ChannelConfig?> GetByIdAsync(Guid id) => Task.FromResult(id == channel.Id ? channel : null);
        public Task<ChannelConfig?> GetDefaultByChannelTypeAsync(ChannelType type) => Unused.Fail<Task<ChannelConfig?>>();
        public Task<ChannelConfig?> GetActiveBySlugAsync(ChannelType type, string labelSlug) => Unused.Fail<Task<ChannelConfig?>>();
        public Task<List<ChannelConfig>> GetAllByChannelTypeAsync(ChannelType type) => Unused.Fail<Task<List<ChannelConfig>>>();
        public Task SetDefaultAsync(Guid id, ChannelType type) => Unused.Fail<Task>();
        public Task<List<ChannelConfig>> GetAllAsync() => Unused.Fail<Task<List<ChannelConfig>>>();
        public Task<List<ChannelConfig>> FindAsync(Expression<Func<ChannelConfig, bool>> p) => Unused.Fail<Task<List<ChannelConfig>>>();
        public Task AddAsync(ChannelConfig entity) => Unused.Fail<Task>();
        public void Update(ChannelConfig entity) => Unused.Fail<bool>();
        public void Delete(ChannelConfig entity) { Deleted = true; }
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
    }

    private sealed class StubFallbackPolicyRepository(List<string> referencingNames) : IFallbackPolicyRepository
    {
        public Task<List<string>> GetNamesReferencingChannelAsync(Guid channelConfigId) => Task.FromResult(referencingNames);
        public Task<List<FallbackPolicy>> GetActivePoliciesAsync() => Unused.Fail<Task<List<FallbackPolicy>>>();
        public Task<FallbackPolicy?> GetActiveByTriggerChannelAsync(ChannelType triggerChannel) => Unused.Fail<Task<FallbackPolicy?>>();
        public Task<FallbackPolicy?> GetByIdAsync(Guid id) => Unused.Fail<Task<FallbackPolicy?>>();
        public Task<List<FallbackPolicy>> GetAllAsync() => Unused.Fail<Task<List<FallbackPolicy>>>();
        public Task<List<FallbackPolicy>> FindAsync(Expression<Func<FallbackPolicy, bool>> p) => Unused.Fail<Task<List<FallbackPolicy>>>();
        public Task AddAsync(FallbackPolicy entity) => Unused.Fail<Task>();
        public void Update(FallbackPolicy entity) => Unused.Fail<bool>();
        public void Delete(FallbackPolicy entity) => Unused.Fail<bool>();
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
    }

    private sealed class StubJobRepository(bool hasHistory) : IDispatchJobRepository
    {
        public Task<bool> ExistsForChannelAsync(Guid channelConfigId) => Task.FromResult(hasHistory);
        public Task<List<DispatchJob>> GetByMessageRequestIdAsync(Guid id) => Unused.Fail<Task<List<DispatchJob>>>();
        public Task<List<DispatchJob>> GetFailedWithChannelAsync() => Unused.Fail<Task<List<DispatchJob>>>();
        public Task<DispatchJob?> GetByIdAsync(Guid id) => Unused.Fail<Task<DispatchJob?>>();
        public Task<List<DispatchJob>> GetAllAsync() => Unused.Fail<Task<List<DispatchJob>>>();
        public Task<List<DispatchJob>> FindAsync(Expression<Func<DispatchJob, bool>> p) => Unused.Fail<Task<List<DispatchJob>>>();
        public Task AddAsync(DispatchJob entity) => Unused.Fail<Task>();
        public void Update(DispatchJob entity) { }
        public void Delete(DispatchJob entity) => Unused.Fail<bool>();
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
    }

    private static ChannelConfig NonDefaultChannel() => new()
    {
        Id = Guid.NewGuid(),
        ChannelType = ChannelType.SMS,
        Provider = Provider.Reve,
        ProviderName = "REVE",
        Label = "SMS Secondary",
        LabelSlug = "smssecondary",
        IsActive = true,
        IsDefault = false,
        ConfigJson = "{}",
    };

    private static ChannelConfigService Build(
        ChannelConfig channel, bool hasHistory, params string[] referencingPolicies)
        => new(
            new SingleChannelRepository(channel),
            new StubFallbackPolicyRepository(referencingPolicies.ToList()),
            new FakeChannelTemplateRepository(),
            new StubJobRepository(hasHistory),
            new PlainTextEncryption(),
            new NoOpGatewayCredentialServiceForDelete());

    private sealed class NoOpGatewayCredentialServiceForDelete : IGatewayCredentialService
    {
        public Task<NetZeroGateway.Application.DTOs.GatewayCredentialDto> GetAsync() => Unused.Fail<Task<NetZeroGateway.Application.DTOs.GatewayCredentialDto>>();
        public Task<NetZeroGateway.Application.DTOs.GatewayCredentialDto> UpdateAsync(NetZeroGateway.Application.DTOs.UpdateGatewayCredentialRequest request) => Unused.Fail<Task<NetZeroGateway.Application.DTOs.GatewayCredentialDto>>();
        public Task SyncUsernameFromDefaultSmsChannelAsync(string? balanceClientId) => Task.CompletedTask;
    }

    [Fact]
    public async Task A_channel_with_no_history_no_policy_and_not_default_deletes_cleanly()
    {
        var channel = NonDefaultChannel();
        var repo = new SingleChannelRepository(channel);
        var service = new ChannelConfigService(
            repo, new StubFallbackPolicyRepository([]), new FakeChannelTemplateRepository(),
            new StubJobRepository(hasHistory: false), new PlainTextEncryption(),
            new NoOpGatewayCredentialServiceForDelete());

        await service.DeleteAsync(channel.Id);

        Assert.True(repo.Deleted);
    }

    [Fact]
    public async Task A_channel_that_has_sent_messages_cannot_be_deleted()
    {
        var channel = NonDefaultChannel();
        var service = Build(channel, hasHistory: true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(channel.Id));

        Assert.Contains("sent message(s)", ex.Message);
        Assert.Contains("Deactivate", ex.Message);
    }

    [Fact]
    public async Task The_default_channel_cannot_be_deleted()
    {
        var channel = NonDefaultChannel();
        channel.IsDefault = true;
        var service = Build(channel, hasHistory: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(channel.Id));

        Assert.Contains("default", ex.Message);
    }

    [Fact]
    public async Task A_channel_referenced_by_a_fallback_policy_cannot_be_deleted()
    {
        var channel = NonDefaultChannel();
        var service = Build(channel, hasHistory: false, "Primary to Backup");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(channel.Id));

        Assert.Contains("Primary to Backup", ex.Message);
    }

    [Fact]
    public async Task History_is_checked_only_after_default_and_policy_do_not_block_it()
    {
        // Order matters for which message the admin sees first: the default/policy
        // checks are cheap and already existed, so they still run first.
        var channel = NonDefaultChannel();
        channel.IsDefault = true;
        var service = Build(channel, hasHistory: true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(channel.Id));

        Assert.Contains("default", ex.Message);
    }
}
