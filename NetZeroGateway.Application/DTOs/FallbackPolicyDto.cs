using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class FallbackPolicyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ChannelType TriggerChannel { get; set; }
    public ChannelType FallbackChannel { get; set; }
    /// <summary>Null = any channel of the trigger type.</summary>
    public Guid? TriggerChannelConfigId { get; set; }
    /// <summary>Null = that type's default channel.</summary>
    public Guid? FallbackChannelConfigId { get; set; }
    /// <summary>Required to let a policy fall back to a different channel type.</summary>
    public bool AllowCrossChannel { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Display labels so the UI need not re-resolve the ids.</summary>
    public string? TriggerChannelLabel { get; set; }
    public string? FallbackChannelLabel { get; set; }
    /// <summary>
    /// Read-only, derived from <see cref="TriggerChannelConfigId"/> being null (hotfix
    /// batch 9, Task 9) — it used to be an admin-set flag independent of scoping, which
    /// defaulted off in the UI and silently hid a correctly-scoped active policy from
    /// fallback resolution entirely. Display only; not accepted on create/update.
    /// </summary>
    public bool IsGlobal { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
