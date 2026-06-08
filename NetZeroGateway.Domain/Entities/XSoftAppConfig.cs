namespace NetZeroGateway.Domain.Entities;

/// <summary>
/// Stores the x-soft calling application's base URL and shared secret.
/// Admin can update these via the admin panel without redeployment.
/// SharedSecretHash: BCrypt hash of the raw shared secret.
/// The x-soft app sends the raw secret in the X-XSoft-Token header.
/// </summary>
public class XSoftAppConfig : BaseEntity
{
    public string  Label             { get; set; } = "x-soft";
    public string  BaseUrl           { get; set; } = string.Empty;
    public string  SharedSecretHash  { get; set; } = string.Empty;
    public bool    IsActive          { get; set; } = true;
    public string? Description       { get; set; }
}
