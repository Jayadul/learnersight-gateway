using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Utilities;

/// <summary>
/// Renders a stored recipient number the way one provider expects to receive it.
/// <para>
/// Numbers are stored canonically — digits only, country code included, no plus
/// (<c>8801700000001</c>). Until Release 3 every provider took that form, so nothing
/// needed converting. The two new SMS vendors disagree with it in opposite directions:
/// GenNet's API takes the local form (<c>01700000001</c>) while NetZero Inbox wants a
/// leading plus (<c>+8801700000001</c>). Chithi, despite sharing GenNet's credentials
/// and API family, takes the canonical form — so the format is a per-provider fact and
/// cannot be inferred from the vendor's platform.
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
        // 01700000001 — GenNet's documented request form.
        Provider.GenNet => ToLocal(canonical),

        // +8801700000001 — NetZero Inbox follows the WhatsApp Cloud API shape.
        Provider.NetZeroInbox => ToPlus(canonical),

        // REVE, WaTrend, Voice and Chithi all take the stored form unchanged.
        _ => canonical
    };

    /// <summary>Drops the country code: <c>8801700000001</c> → <c>01700000001</c>.</summary>
    public static string ToLocal(string canonical)
    {
        var digits = Digits(canonical);
        return digits.StartsWith(CountryCode, StringComparison.Ordinal)
            ? "0" + digits[CountryCode.Length..]
            : digits;
    }

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
