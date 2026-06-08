using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Domain.Entities;

public class FallbackPolicy : BaseEntity
{
    public string      Name            { get; set; } = string.Empty;
    public ChannelType TriggerChannel  { get; set; }
    public ChannelType FallbackChannel { get; set; }
    public bool        IsActive        { get; set; } = true;
    public bool        IsGlobal        { get; set; } = true;
    public string?     Description     { get; set; }
}
