namespace NetZeroGateway.Application.DTOs;

/// <summary>
/// The gateway credentials as the admin panel sees them, decrypted.
/// <para>
/// Unlike a channel config, these are read back in clear. A SuperAdmin has to be able to
/// hand the exact pair to an integrator, and there is nowhere else to look them up — the
/// column is ciphertext and the seed value goes stale the moment anyone rotates. The
/// endpoint is SuperAdmin-only and the UI keeps both fields hidden behind a reveal
/// toggle, so the values are available on purpose rather than on display.
/// </para>
/// </summary>
public class GatewayCredentialDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    /// <summary>Decrypted. Empty when the stored ciphertext could not be read.</summary>
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>Decrypted. Empty when the stored ciphertext could not be read.</summary>
    public string SecretKey { get; set; } = string.Empty;
    /// <summary>
    /// False when the stored keys could not be decrypted — almost always a changed
    /// encryption key. The UI says so rather than showing two empty boxes that look like
    /// missing credentials.
    /// </summary>
    public bool IsReadable { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
