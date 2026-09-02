namespace NetZeroGateway.Domain.Entities;

/// <summary>
/// The credentials a third-party integrator presents to <c>/sendtext</c>. One row: the
/// gateway has a single set of keys, independent of any channel.
/// <para>
/// Until Release 2.1 these were read off the default SMS <see cref="ChannelConfig"/>.
/// Once several SMS channels could be active at once that was ambiguous — and it tied
/// every integrator's access to one provider account. See
/// <c>docs/adr/0007-decouple-gateway-auth-from-channel-config.md</c>.
/// </para>
/// </summary>
public class GatewayCredential : BaseEntity
{
    /// <summary>
    /// Names who the credentials belong to. Recorded for the admin panel only — the
    /// gateway does not ask callers for it, so renaming it cannot break an integration.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>AES-256 encrypted, same scheme as <see cref="ChannelConfig.ConfigJson"/>.</summary>
    public string ApiKeyEncrypted { get; set; } = string.Empty;

    /// <summary>AES-256 encrypted, same scheme as <see cref="ChannelConfig.ConfigJson"/>.</summary>
    public string SecretKeyEncrypted { get; set; } = string.Empty;
}
