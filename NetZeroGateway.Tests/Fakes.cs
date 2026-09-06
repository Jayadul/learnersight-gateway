using System.Linq.Expressions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// Hand-rolled test doubles. Deliberately no mocking library — these interfaces are
/// small, and members a test does not exercise throw so accidental reliance is loud
/// rather than silently returning null.
/// </summary>
internal static class Unused
{
    public static T Fail<T>([System.Runtime.CompilerServices.CallerMemberName] string member = "")
        => throw new NotSupportedException($"{member} is not exercised by these tests.");
}

/// <summary>Identity encryption so fixtures can hold readable JSON.</summary>
internal sealed class PlainTextEncryption : IEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
}

/// <summary>Records the config dictionary the dispatcher hands to the sender.</summary>
internal sealed class CapturingSender(
    ChannelType channelType, Provider provider = Provider.Reve) : IChannelSender
{
    public ChannelType ChannelType { get; } = channelType;
    public Provider Provider { get; } = provider;
    public Dictionary<string, string>? CapturedConfig { get; private set; }
    public DispatchJob? CapturedJob { get; private set; }

    public Task SendAsync(
        DispatchJob job, Dictionary<string, string> config, CancellationToken ct = default)
    {
        // Snapshot: the dispatcher may mutate the same instance for later jobs.
        CapturedConfig = new Dictionary<string, string>(config);
        CapturedJob = job;
        job.Status = JobStatus.Sent;
        return Task.CompletedTask;
    }
}

internal sealed class FakeSenderFactory(IChannelSender sender) : IChannelSenderFactory
{
    // provider and hasMedia are ignored: these tests supply one sender and assert on
    // what reaches it. Factory resolution has its own tests.
    public IChannelSender GetSender(
        ChannelType channelType, Provider provider, bool hasMedia = false) => sender;
}

internal sealed class FakeChannelConfigRepository(ChannelConfig active) : IChannelConfigRepository
{
    public Task<ChannelConfig?> GetDefaultByChannelTypeAsync(ChannelType type)
        => Task.FromResult<ChannelConfig?>(active);

    /// <summary>Any slug resolves to the one fixture config; slug routing has its own tests.</summary>
    public Task<ChannelConfig?> GetActiveBySlugAsync(ChannelType type, string labelSlug)
        => Task.FromResult<ChannelConfig?>(active);

    public Task<List<ChannelConfig>> GetAllByChannelTypeAsync(ChannelType type) => Unused.Fail<Task<List<ChannelConfig>>>();
    public Task SetDefaultAsync(Guid id, ChannelType type) => Unused.Fail<Task>();
    public Task<ChannelConfig?> GetByIdAsync(Guid id) => Task.FromResult<ChannelConfig?>(active);
    public Task<List<ChannelConfig>> GetAllAsync() => Unused.Fail<Task<List<ChannelConfig>>>();
    public Task<List<ChannelConfig>> FindAsync(Expression<Func<ChannelConfig, bool>> p) => Unused.Fail<Task<List<ChannelConfig>>>();
    public Task AddAsync(ChannelConfig entity) => Unused.Fail<Task>();
    public void Update(ChannelConfig entity) => Unused.Fail<bool>();
    public void Delete(ChannelConfig entity) => Unused.Fail<bool>();
    public Task<int> SaveChangesAsync() => Task.FromResult(0);
}

internal sealed class FakeDispatchJobRepository : IDispatchJobRepository
{
    public List<DispatchJob> Added { get; } = [];

    public Task AddAsync(DispatchJob entity)
    {
        Added.Add(entity);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync() => Task.FromResult(0);

    public Task<List<DispatchJob>> GetByMessageRequestIdAsync(Guid id) => Unused.Fail<Task<List<DispatchJob>>>();
    public Task<List<DispatchJob>> GetFailedWithChannelAsync() => Unused.Fail<Task<List<DispatchJob>>>();
    public Task<(List<DispatchJob> Items, int Total)> GetPagedAsync(
        int page, int size, DateTimeOffset? from, DateTimeOffset? to,
        ChannelType? channelType, JobStatus? status, string? toNumber,
        DeliveryStatus? deliveryStatus = null)
        => Unused.Fail<Task<(List<DispatchJob>, int)>>();
    public Task<DispatchJob?> GetByIdAsync(Guid id) => Unused.Fail<Task<DispatchJob?>>();
    public Task<List<DispatchJob>> GetAllAsync() => Unused.Fail<Task<List<DispatchJob>>>();
    public Task<List<DispatchJob>> FindAsync(Expression<Func<DispatchJob, bool>> p) => Unused.Fail<Task<List<DispatchJob>>>();
    public void Update(DispatchJob entity) { }
    public void Delete(DispatchJob entity) => Unused.Fail<bool>();
}

/// <summary>
/// Collects the per-recipient rows the dispatcher writes after a send, so tests can
/// assert one row per number without a database.
/// </summary>
internal sealed class FakeDispatchJobRecipientRepository : IDispatchJobRecipientRepository
{
    public List<DispatchJobRecipient> Added { get; } = [];

    public Task AddRangeAsync(IEnumerable<DispatchJobRecipient> recipients)
    {
        Added.AddRange(recipients);
        return Task.CompletedTask;
    }

    public Task AddAsync(DispatchJobRecipient entity)
    {
        Added.Add(entity);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync() => Task.FromResult(0);

    public Task<List<DispatchJobRecipient>> GetByDispatchJobIdAsync(Guid dispatchJobId)
        => Task.FromResult(Added.Where(r => r.DispatchJobId == dispatchJobId).ToList());

    public Task<List<DispatchJobRecipient>> GetPendingForChannelAsync(
        Guid channelConfigId, DateTimeOffset from, DateTimeOffset to, int limit)
        => Unused.Fail<Task<List<DispatchJobRecipient>>>();

    public Task<DispatchJobRecipient?> GetByIdAsync(Guid id) => Unused.Fail<Task<DispatchJobRecipient?>>();
    public Task<List<DispatchJobRecipient>> GetAllAsync() => Unused.Fail<Task<List<DispatchJobRecipient>>>();
    public Task<List<DispatchJobRecipient>> FindAsync(Expression<Func<DispatchJobRecipient, bool>> p)
        => Unused.Fail<Task<List<DispatchJobRecipient>>>();
    public void Update(DispatchJobRecipient entity) { }
    public void Delete(DispatchJobRecipient entity) => Unused.Fail<bool>();
}

/// <summary>
/// GetWithJobsAsync returns null on purpose: ChannelDispatchService treats that as
/// "nothing to summarise" and returns early, keeping these tests focused on dispatch.
/// </summary>
internal sealed class FakeMessageRequestRepository(MessageRequest? byId = null) : IMessageRequestRepository
{
    public Task<MessageRequest?> GetWithJobsAsync(Guid id) => Task.FromResult<MessageRequest?>(null);
    public Task<int> SaveChangesAsync() => Task.FromResult(0);

    /// <summary>The request the fallback handler reads its multi-channel flag from.</summary>
    public Task<MessageRequest?> GetByIdAsync(Guid id) => Task.FromResult(byId);

    public Task<(List<MessageRequest> Items, int Total)> GetPagedAsync(
        int page, int size, DateTimeOffset? from, DateTimeOffset? to,
        string? toUser, MessageStatus? status, MessageSource? source,
        Guid? channelConfigId)
        => Unused.Fail<Task<(List<MessageRequest>, int)>>();
    public Task<List<MessageRequest>> GetAllAsync() => Unused.Fail<Task<List<MessageRequest>>>();
    public Task<List<MessageRequest>> FindAsync(Expression<Func<MessageRequest, bool>> p) => Unused.Fail<Task<List<MessageRequest>>>();
    public Task AddAsync(MessageRequest entity) => Unused.Fail<Task>();
    public void Update(MessageRequest entity) { }
    public void Delete(MessageRequest entity) => Unused.Fail<bool>();
}

internal sealed class NoOpFallbackHandler : IFallbackHandlerService
{
    public Task HandleAsync(Guid messageRequestId) => Task.CompletedTask;
}

internal sealed class FakeFallbackPolicyRepository(params FallbackPolicy[] policies)
    : IFallbackPolicyRepository
{
    public Task<List<FallbackPolicy>> GetActiveGlobalPoliciesAsync()
        => Task.FromResult(policies.Where(p => p.IsActive && p.IsGlobal).ToList());

    public Task<FallbackPolicy?> GetActiveByTriggerChannelAsync(ChannelType triggerChannel)
        => Unused.Fail<Task<FallbackPolicy?>>();
    public Task<List<string>> GetNamesReferencingChannelAsync(Guid channelConfigId)
        => Unused.Fail<Task<List<string>>>();
    public Task<FallbackPolicy?> GetByIdAsync(Guid id) => Unused.Fail<Task<FallbackPolicy?>>();
    public Task<List<FallbackPolicy>> GetAllAsync() => Unused.Fail<Task<List<FallbackPolicy>>>();
    public Task<List<FallbackPolicy>> FindAsync(Expression<Func<FallbackPolicy, bool>> p) => Unused.Fail<Task<List<FallbackPolicy>>>();
    public Task AddAsync(FallbackPolicy entity) => Unused.Fail<Task>();
    public void Update(FallbackPolicy entity) => Unused.Fail<bool>();
    public void Delete(FallbackPolicy entity) => Unused.Fail<bool>();
    public Task<int> SaveChangesAsync() => Task.FromResult(0);
}

/// <summary>Returns the jobs a request already has, and records anything added.</summary>
internal sealed class FakeJobRepositoryWithExisting(List<DispatchJob> existing) : IDispatchJobRepository
{
    public List<DispatchJob> Added { get; } = [];

    public Task<List<DispatchJob>> GetByMessageRequestIdAsync(Guid id) => Task.FromResult(existing);
    public Task<List<DispatchJob>> GetFailedWithChannelAsync() => Unused.Fail<Task<List<DispatchJob>>>();

    public Task AddAsync(DispatchJob entity)
    {
        Added.Add(entity);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync() => Task.FromResult(0);

    public Task<(List<DispatchJob> Items, int Total)> GetPagedAsync(
        int page, int size, DateTimeOffset? from, DateTimeOffset? to,
        ChannelType? channelType, JobStatus? status, string? toNumber,
        DeliveryStatus? deliveryStatus = null)
        => Unused.Fail<Task<(List<DispatchJob>, int)>>();
    public Task<DispatchJob?> GetByIdAsync(Guid id) => Unused.Fail<Task<DispatchJob?>>();
    public Task<List<DispatchJob>> GetAllAsync() => Unused.Fail<Task<List<DispatchJob>>>();
    public Task<List<DispatchJob>> FindAsync(Expression<Func<DispatchJob, bool>> p) => Unused.Fail<Task<List<DispatchJob>>>();
    public void Update(DispatchJob entity) { }
    public void Delete(DispatchJob entity) => Unused.Fail<bool>();
}
