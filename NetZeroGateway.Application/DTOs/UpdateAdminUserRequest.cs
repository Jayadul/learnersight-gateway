using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class UpdateAdminUserRequest
{
    public string    Email       { get; set; } = string.Empty;
    public AdminRole Role        { get; set; }
    public bool      IsActive    { get; set; }
    public string?   NewPassword { get; set; }
}
