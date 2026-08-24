namespace NetZeroGateway.Domain.Enums;

/// <summary>
/// Wire format is the integer value — see docs/api.md.
/// <para>
/// 2 is intentionally skipped. It was <c>WP_Media</c> before WhatsApp text and media
/// were unified into a single <c>WP</c> channel; media is now distinguished by whether
/// a job carries a MediaUrl, not by channel type. Voice deliberately keeps 3 so
/// integrators sending Voice are unaffected — do not renumber to close the gap.
/// </para>
/// </summary>
public enum ChannelType { SMS = 0, WP = 1, Voice = 3 }
