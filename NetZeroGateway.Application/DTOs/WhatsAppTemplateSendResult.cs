namespace NetZeroGateway.Application.DTOs;

/// <summary>Outcome of one WhatsApp Template send (hotfix batch 3), provider-agnostic.</summary>
public class WhatsAppTemplateSendResult
{
    public bool IsSuccess { get; set; }
    public string? MessageId { get; set; }
    public string? Error { get; set; }
    /// <summary>The exact raw provider response, stored verbatim on the dispatch job.</summary>
    public string RawResponse { get; set; } = string.Empty;
}
