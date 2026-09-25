using System.Text.Json;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Infrastructure.HttpClients;

namespace NetZeroGateway.Infrastructure.Strategies;

/// <summary>
/// Infrastructure-side implementation of <see cref="IWhatsAppTemplateSender"/> — the only
/// place that touches <see cref="INetZeroInboxClient"/> for a template send, keeping the
/// Application layer's dependency on an interface it owns rather than the HTTP client.
/// </summary>
public class NetZeroInboxTemplateSender(INetZeroInboxClient client) : IWhatsAppTemplateSender
{
    public async Task<WhatsAppTemplateSendResult> SendAsync(
        string templateToken, string toNumber, string templateName,
        List<string> exampleArr, string? mediaUrl, string? baseUrl,
        CancellationToken ct = default)
    {
        var r = await client.SendTemplateAsync(
            templateToken, toNumber, templateName, exampleArr, mediaUrl, baseUrl, ct);

        return new WhatsAppTemplateSendResult
        {
            IsSuccess = r.IsSuccess,
            MessageId = r.MessageId,
            Error = r.IsSuccess ? null : (r.FailureReason ?? "unknown_error"),
            RawResponse = JsonSerializer.Serialize(r)
        };
    }
}
