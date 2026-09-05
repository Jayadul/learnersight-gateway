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

    /// <summary>
    /// NetZero Inbox WhatsApp, structured v1 API
    /// (<c>POST https://inbox.netzero.com.bd/api/v1/send-message</c>).
    /// </summary>
    NetZeroInbox = 4,

    /// <summary>Chithi SMS (sms.chithi.net).</summary>
    Chithi = 5,

    /// <summary>
    /// NetZero Inbox WhatsApp, QR-linked device API
    /// (<c>GET http://inbox.netzero.com.bd/api/qr/rest/send_message</c>).
    /// <para>
    /// A separate member rather than a mode on <see cref="NetZeroInbox"/>: it is a GET
    /// with query parameters over plain http where v1 is a JSON POST over https, it
    /// carries a <c>from</c> number that v1 has none of, and it takes the recipient
    /// without the leading plus that v1 requires. Only the token is shared.
    /// </para>
    /// </summary>
    NetZeroInboxQr = 6
}
