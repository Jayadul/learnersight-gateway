using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Domain.Entities;

public class AdminUser : BaseEntity
{
    public string          Username    { get; set; } = string.Empty;
    public string          PasswordHash { get; set; } = string.Empty;
    public string          Email       { get; set; } = string.Empty;
    public AdminRole       Role        { get; set; }
    public bool            IsActive    { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }
}
