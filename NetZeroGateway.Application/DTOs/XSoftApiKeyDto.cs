namespace NetZeroGateway.Application.DTOs;
public class XSoftApiKeyDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
