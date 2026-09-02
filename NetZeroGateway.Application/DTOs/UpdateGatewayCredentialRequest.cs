namespace NetZeroGateway.Application.DTOs;

/// <summary>
/// Every field is optional and every omitted field keeps its stored value. Sending one
/// field must not blank the other two — the Release 2 channel-update bug, which sent a
/// whole object and wiped whatever the form had not loaded.
/// </summary>
public class UpdateGatewayCredentialRequest
{
    public string? Username { get; set; }
    /// <summary>Ignored when it arrives as the mask the read endpoint returned.</summary>
    public string? ApiKey { get; set; }
    /// <summary>Ignored when it arrives as the mask the read endpoint returned.</summary>
    public string? SecretKey { get; set; }
}
