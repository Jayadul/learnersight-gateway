using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public interface INetZeroInboxClient
{
    /// <summary>
    /// Sends one WhatsApp text message. The recipient must already carry a leading plus
    /// — see RecipientFormat. Media is not implemented: the vendor has not documented a
    /// type value for it, and guessing a request shape is worse than a clear rejection.
    /// </summary>
    Task<NetZeroInboxResponse> SendTextAsync(
        string token, string to, string message,
        string? baseUrl = null,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a Meta pre-approved WhatsApp template (hotfix batch 3) — a distinct v1 API
    /// action from <see cref="SendTextAsync"/>, its own token, and a structured payload
    /// (template name + ordered variables) rather than a free-text message. Admin-only,
    /// triggered from Manual Send — never reachable via /sendtext's tag grammar, since a
    /// template send does not fit a free-text tag.
    /// </summary>
    Task<NetZeroInboxTemplateResponse> SendTemplateAsync(
        string templateToken, string sendTo, string templateName,
        List<string> exampleArr, string? mediaUri,
        string? baseUrl = null,
        CancellationToken ct = default);
}
