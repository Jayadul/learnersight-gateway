using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Utilities;

/// <summary>
/// Renders a stored recipient number the way one provider expects to receive it.
/// <para>
/// Numbers are stored canonically — digits only, country code included, no plus
/// (<c>8801700000001</c>). Until Release 3 every provider took that form, so nothing
/// needed converting. NetZero Inbox disagrees with it: it wants a leading plus
/// (<c>+8801700000001</c>). Chithi, despite sharing GenNet's old credentials and API
/// family before GenNet was retired, takes the canonical form — so the format is a
/// per-provider fact and cannot be inferred from the vendor's platform.
/// </para>
/// <para>
/// Only the request side is converted. Providers echo numbers back in their own shape,
/// and matching a response to a stored recipient uses <see cref="ToCanonical"/>.
/// </para>
/// </summary>
public static class RecipientFormat
{
    /// <summary>
    /// Bangladesh country code. It is <c>880</c>, not <c>88</c> — a stored
    /// <c>8801748848039</c> is <c>880</c> + the subscriber number <c>1748848039</c>, and
    /// the local form puts the trunk <c>0</c> in front of that: <c>01748848039</c>.
    /// Treating the code as <c>88</c> leaves the local number with two leading zeros,
    /// which is a different number. The gateway is Bangladesh-first — see 1.0.0.
    /// </summary>
    private const string CountryCode = "880";

    /// <summary>How a provider wants a recipient written on the wire.</summary>
    public static string For(Provider provider, string canonical) => provider switch
    {
        // +8801700000001 — NetZero Inbox follows the WhatsApp Cloud API shape.
        Provider.NetZeroInbox => ToPlus(canonical),

        // REVE, WaTrend, Voice, Chithi and NetZero Inbox's QR API all take the stored
        // form unchanged. The QR case is listed explicitly because the same vendor's v1
        // API above does not, and the two are easy to conflate.
        Provider.NetZeroInboxQr => canonical,

        _ => canonical
    };

    /// <summary>Prefixes a plus: <c>8801700000001</c> → <c>+8801700000001</c>.</summary>
    public static string ToPlus(string canonical) => "+" + Digits(canonical);

    /// <summary>
    /// Maps any provider's rendering back to the stored form, so a response can be
    /// matched to the recipient row it belongs to.
    /// </summary>
    public static string ToCanonical(string value)
    {
        var digits = Digits(value);
        if (digits.StartsWith(CountryCode, StringComparison.Ordinal)) return digits;

        // Local form: 01700000001 → 8801700000001. The leading zero is the trunk prefix
        // and is replaced by the country code, not kept alongside it.
        return digits.StartsWith('0') ? CountryCode + digits[1..] : digits;
    }

    /// <summary>
    /// Strips everything that is not a digit. A stored number is digits already — a
    /// client red line — but a provider's echo may carry a plus or separators.
    /// </summary>
    private static string Digits(string value)
        => new([.. value.Where(char.IsAsciiDigit)]);
}
