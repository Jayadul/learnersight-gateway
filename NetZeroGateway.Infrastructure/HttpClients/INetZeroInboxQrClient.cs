using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public interface INetZeroInboxQrClient
{
    /// <summary>
    /// Sends one WhatsApp text through a QR-linked device. Everything travels in the
    /// query string, including the token and the message text. The recipient takes the
    /// stored form with no leading plus — unlike the v1 API on the same host.
    /// </summary>
    Task<NetZeroInboxQrResponse> SendTextAsync(
        string token, string from, string to, string message,
        string? baseUrl = null,
        CancellationToken ct = default);

    /// <summary>
    /// Sends one image through a QR-linked device (<c>messageType=image</c>). Confirmed
    /// working against the vendor's API; other media types are not yet documented and
    /// must not be routed here — see <see cref="NetZeroInboxQrWhatsAppSender"/>.
    /// </summary>
    Task<NetZeroInboxQrResponse> SendImageAsync(
        string token, string from, string to, string imageUrl, string caption,
        string? baseUrl = null,
        CancellationToken ct = default);
}
