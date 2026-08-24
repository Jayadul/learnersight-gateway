namespace NetZeroGateway.Application.DTOs;

/// <summary>
/// Outcome of reading the channel tags out of a message. An unrecognised tag fails the
/// whole request rather than being dropped: a silently skipped tag produces no job and
/// no error, which looks like a successful send that delivered nothing.
/// </summary>
public sealed class ParsedTemplate
{
    public List<ParsedChannelJob> Jobs { get; init; } = [];

    /// <summary>Null when the message parsed cleanly; otherwise the reason to reject it.</summary>
    public string? Error { get; init; }

    public bool IsValid => Error is null;

    public static ParsedTemplate Ok(List<ParsedChannelJob> jobs) => new() { Jobs = jobs };
    public static ParsedTemplate Invalid(string error) => new() { Error = error };
}
