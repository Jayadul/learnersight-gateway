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
internal sealed class CapturingSender(ChannelType channelType) : IChannelSender
{
    public ChannelType ChannelType { get; } = channelType;
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
    public IChannelSender GetSender(ChannelType channelType) => sender;
}

internal sealed class FakeChannelConfigRepository(ChannelConfig active) : IChannelConfigRepository
{
    public Task<ChannelConfig?> GetActiveByChannelTypeAsync(ChannelType type)
        => Task.FromResult<ChannelConfig?>(active);

    public Task<List<ChannelConfig>> GetAllByChannelTypeAsync(ChannelType type) => Unused.Fail<Task<List<ChannelConfig>>>();
    public Task ActivateConfigAsync(Guid id, ChannelType type) => Unused.Fail<Task>();
    public Task<ChannelConfig?> GetByIdAsync(Guid id) => Unused.Fail<Task<ChannelConfig?>>();
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
    public Task<(List<DispatchJob> Items, int Total)> GetPagedAsync(
        int page, int size, DateTimeOffset? from, DateTimeOffset? to,
        ChannelType? channelType, JobStatus? status, string? toNumber)
        => Unused.Fail<Task<(List<DispatchJob>, int)>>();
    public Task<DispatchJob?> GetByIdAsync(Guid id) => Unused.Fail<Task<DispatchJob?>>();
    public Task<List<DispatchJob>> GetAllAsync() => Unused.Fail<Task<List<DispatchJob>>>();
    public Task<List<DispatchJob>> FindAsync(Expression<Func<DispatchJob, bool>> p) => Unused.Fail<Task<List<DispatchJob>>>();
    public void Update(DispatchJob entity) { }
    public void Delete(DispatchJob entity) => Unused.Fail<bool>();
}

/// <summary>
/// GetWithJobsAsync returns null on purpose: ChannelDispatchService treats that as
/// "nothing to summarise" and returns early, keeping these tests focused on dispatch.
/// </summary>
internal sealed class FakeMessageRequestRepository : IMessageRequestRepository
{
    public Task<MessageRequest?> GetWithJobsAsync(Guid id) => Task.FromResult<MessageRequest?>(null);
    public Task<int> SaveChangesAsync() => Task.FromResult(0);

    public Task<(List<MessageRequest> Items, int Total)> GetPagedAsync(
        int page, int size, DateTimeOffset? from, DateTimeOffset? to,
        string? toUser, MessageStatus? status, MessageSource? source)
        => Unused.Fail<Task<(List<MessageRequest>, int)>>();
    public Task<MessageRequest?> GetByIdAsync(Guid id) => Unused.Fail<Task<MessageRequest?>>();
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
