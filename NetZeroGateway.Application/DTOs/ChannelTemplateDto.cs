namespace NetZeroGateway.Application.DTOs;

/// <summary>A template registered on a Template channel (hotfix batch 9, Task 1).</summary>
public class ChannelTemplateDto
{
    public Guid Id { get; set; }
    public Guid ChannelConfigId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> VariableLabels { get; set; } = [];
    public int VariableCount => VariableLabels.Count;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class CreateChannelTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public List<string> VariableLabels { get; set; } = [];
}

public class UpdateChannelTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public List<string> VariableLabels { get; set; } = [];
}
