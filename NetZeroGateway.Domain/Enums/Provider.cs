namespace NetZeroGateway.Domain.Enums;

/// <summary>
/// The vendor behind a channel config. Until Release 3 a channel type implied its
/// provider — SMS meant REVE, WhatsApp meant WaTrend — so nothing needed to name one.
/// Several providers now serve the same channel type, which makes this part of the
/// sender-resolution key alongside <see cref="ChannelType"/>. See ADR 0008.
/// <para>
/// Wire format is the integer value; never renumber a shipped member.
/// </para>
/// </summary>
public enum Provider
{
    /// <summary>REVE SMS (smpp.revesms.com) — the original SMS provider.</summary>
    Reve = 0,

    /// <summary>WaTrend (app.watrend.com) — the original WhatsApp provider.</summary>
    WaTrend = 1,

    /// <summary>
    /// NetZero voice campaigns (api.sms.netzero.com.bd). Named in full to keep it
    /// distinct from <see cref="NetZeroInbox"/>, which is an unrelated NetZero-branded
    /// service — see ADR 0008.
    /// </summary>
    NetZeroVoice = 2,

    /// <summary>GenNet SMS (isms.gennet.com.bd).</summary>
    GenNet = 3,

    /// <summary>NetZero Inbox WhatsApp (inbox.netzero.com.bd).</summary>
    NetZeroInbox = 4,

    /// <summary>Chithi SMS (sms.chithi.net).</summary>
    Chithi = 5
}
