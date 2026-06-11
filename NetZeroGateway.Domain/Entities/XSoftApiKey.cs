namespace NetZeroGateway.Domain.Entities;

public class XSoftApiKey : BaseEntity
{
    public string ApiKeyHash { get; set; } = string.Empty;
    public string SecretKeyHash { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastUsedAt { get; set; }
}
