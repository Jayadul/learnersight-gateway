using System.Security.Cryptography;

namespace NetZeroGateway.Application.Utilities;

/// <summary>
/// Generates the client-side message identifier GenNet and Chithi require on every send
/// (<c>batch_csms_id</c> and <c>csms_id</c>).
/// <para>
/// The length is not cosmetic. GenNet rejects anything that is not exactly 20 characters
/// with <c>status_code 4020, "Invalid CSMS ID"</c> — and it does so with HTTP 200, so it
/// reads as a delivery failure rather than a malformed request. A 32-character GUID,
/// which is the obvious thing to reach for, fails every send.
/// </para>
/// </summary>
public static class CsmsId
{
    /// <summary>Exactly what GenNet accepts; Chithi accepts this too.</summary>
    public const int Length = 20;

    /// <summary>
    /// Uppercase letters and digits only, matching the vendors' own examples. Lowercase
    /// and punctuation are untested against their validator, so they are not risked.
    /// </summary>
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>
    /// A fresh identifier. Cryptographically random rather than sequential: it must be
    /// unique per send, and there is no counter to persist across restarts. At 36^20 the
    /// collision risk is not worth a round trip to check.
    /// </summary>
    public static string New() => RandomNumberGenerator.GetString(Alphabet, Length);
}
