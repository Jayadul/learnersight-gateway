using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Strategies;

/// <summary>
/// Strategy interface for dispatching a message through one channel.
/// One implementation per (ChannelType, Provider, HandlesMedia) combination. Open/closed
/// — add a provider by adding an implementation and registering it, without touching
/// existing code. See ADR 0008.
/// </summary>
public interface IChannelSender
{
    ChannelType ChannelType { get; }

    /// <summary>
    /// The vendor this sender talks to. Two senders may share a ChannelType — REVE and
    /// Chithi both send SMS — so the type alone no longer identifies an implementation.
    /// </summary>
    Provider Provider { get; }

    /// <summary>
    /// Whether this sender handles jobs that carry a MediaUrl. WhatsApp text and media
    /// share one ChannelType and one provider, so this is the third part of the key.
    /// </summary>
    bool HandlesMedia => false;

    /// <summary>
    /// Send the job to the provider. Updates job.Status, ProviderRawResponse,
    /// SentAt, ProviderMessageId, and FailureReason in place.
    /// Never throws — all exceptions caught and reflected in job.Status.
    /// </summary>
    Task SendAsync(
        DispatchJob job,
        Dictionary<string, string> config,
        CancellationToken ct = default);
}
