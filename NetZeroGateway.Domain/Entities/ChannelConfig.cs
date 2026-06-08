using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Domain.Entities;

public class ChannelConfig : BaseEntity
{
    public ChannelType ChannelType   { get; set; }
    public string      ProviderName  { get; set; } = string.Empty;
    public string      Label         { get; set; } = string.Empty;
    public bool        IsActive      { get; set; } = true;
    /// <summary>AES-256 encrypted JSON string.</summary>
    public string      ConfigJson    { get; set; } = string.Empty;
}
