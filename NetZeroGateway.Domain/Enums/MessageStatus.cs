namespace NetZeroGateway.Domain.Enums;

/// <summary>
/// Appended to, never renumbered — the wire format sends these as integers and the admin
/// UI maps them by value. Fallback is 4 because 0-3 shipped in Release 1.0.0.
/// </summary>
public enum MessageStatus { Pending = 0, PartialSuccess = 1, Success = 2, Failed = 3, Fallback = 4 }
