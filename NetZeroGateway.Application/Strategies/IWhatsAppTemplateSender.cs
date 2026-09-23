using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Strategies;

/// <summary>
/// Sends a Meta pre-approved WhatsApp template through NetZero Inbox (hotfix batch 3).
/// Deliberately its own small interface rather than a widened <c>IChannelSender</c>: a
/// template send does not go through the dispatch/fallback pipeline
/// (<see cref="IChannelSenderFactory"/> resolves on ChannelType+Provider+HandlesMedia,
/// which has no slot for "template vs freeform text", and bolting one on risks two
/// senders colliding on the same key) or need fallback/retry semantics — it is an
/// admin-triggered, one-off provider call, closer to <c>IChannelBalanceProvider</c> than
/// to a dispatch sender.
/// </summary>
public interface IWhatsAppTemplateSender
{
    Task<WhatsAppTemplateSendResult> SendAsync(
        string templateToken, string toNumber, string templateName,
        List<string> exampleArr, string? mediaUrl, string? baseUrl,
        CancellationToken ct = default);
}
