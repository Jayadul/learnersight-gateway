namespace NetZeroGateway.Application.DTOs;
public class GenerateApiKeyResponse
{
    public Guid   Id        { get; set; }
    public string ApiKey    { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Label     { get; set; } = string.Empty;
    public string Note      { get; set; } = "Store these credentials securely — they will not be shown again.";
}
