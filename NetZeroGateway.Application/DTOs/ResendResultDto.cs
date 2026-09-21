namespace NetZeroGateway.Application.DTOs;

/// <summary>Outcome of a resend, per recipient so a partial failure is legible.</summary>
public class ResendResultDto
{
    public int Requested { get; set; }
    public int Resent { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public List<ResendRecipientOutcome> Outcomes { get; set; } = [];
}

public class ResendRecipientOutcome
{
    public Guid RecipientId { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public bool Sent { get; set; }

    /// <summary>
    /// Whether a send was actually attempted (hotfix batch 4) — false for a recipient
    /// skipped before any provider call, e.g. an inactive channel with no override given.
    /// The recipient row itself is updated in place on every attempt rather than a new
    /// row being created, so this is what distinguishes "tried and failed" from "never
    /// tried" now that there is no new row id to infer it from.
    /// </summary>
    public bool Attempted { get; set; }

    /// <summary>Why it was not sent, or why the provider rejected it.</summary>
    public string? Reason { get; set; }
}
