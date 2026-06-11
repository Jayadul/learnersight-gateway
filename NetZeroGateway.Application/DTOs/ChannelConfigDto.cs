using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class ChannelConfigDto
{
    public Guid Id { get; set; }
    public ChannelType ChannelType { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Dictionary<string, string> ConfigFields { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
