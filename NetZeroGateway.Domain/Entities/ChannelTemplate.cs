namespace NetZeroGateway.Domain.Entities;

/// <summary>
/// A Meta pre-approved WhatsApp template registered on a Template channel (hotfix batch
/// 9, Task 1) — until now a template name was free text typed at send time with nothing
/// backing it, so Manual Send and the <c>template</c> /sendtext tag (Task 10) had no way
/// to offer a dropdown or catch a typo before it reached the vendor.
/// </summary>
public class ChannelTemplate : BaseEntity
{
    public Guid ChannelConfigId { get; set; }
    public ChannelConfig ChannelConfig { get; set; } = null!;

    /// <summary>Must exactly match a name already approved by Meta — free text, not validated against the vendor.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Ordered, human-readable labels for each variable the template expects — reference
    /// text for the admin only (the vendor's own request only takes a positional array,
    /// see NetZeroInboxClient), so the count of labels also doubles as the expected
    /// variable count shown in Manual Send and used to validate the <c>template</c> tag.
    /// </summary>
    public List<string> VariableLabels { get; set; } = [];
}
