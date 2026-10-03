using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Strategies;

/// <summary>
/// Sends a Meta pre-approved WhatsApp template through NetZero Inbox (hotfix batch 3;
/// reachable via the <c>template</c> /sendtext tag since hotfix batch 9, Task 10, in
/// addition to Manual Send). Deliberately its own small interface rather than a widened
/// <c>IChannelSender</c>: a template send never goes through <see cref="IChannelSenderFactory"/>
/// (which resolves on ChannelType+Provider+HandlesMedia, with no slot for "template vs
/// freeform text", and bolting one on risks two senders colliding on the same key) or
/// need fallback/retry semantics — callers (ManualSendService, ChannelDispatchService)
/// skip FallbackHandlerService for it explicitly rather than relying on it to fail safe.
/// </summary>
public interface IWhatsAppTemplateSender
{
    Task<WhatsAppTemplateSendResult> SendAsync(
        string templateToken, string toNumber, string templateName,
        List<string> exampleArr, string? mediaUrl, string? baseUrl,
        CancellationToken ct = default);
}
