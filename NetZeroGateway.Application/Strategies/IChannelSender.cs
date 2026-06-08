using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Strategies;

/// <summary>
/// Strategy interface for dispatching a message through one channel.
/// One implementation per ChannelType. Open/closed — add new channels
/// by adding a new IChannelSender implementation without touching existing code.
/// </summary>
public interface IChannelSender
{
    ChannelType ChannelType { get; }

    /// <summary>
    /// Send the job to the provider. Updates job.Status, ProviderRawResponse,
    /// SentAt, ProviderMessageId, and FailureReason in place.
    /// Never throws — all exceptions caught and reflected in job.Status.
    /// </summary>
    Task SendAsync(
        DispatchJob                 job,
        Dictionary<string, string>  config,
        CancellationToken           ct = default);
}
