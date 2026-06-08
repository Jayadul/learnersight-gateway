using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class CreateAdminUserRequest
{
    public string    Username { get; set; } = string.Empty;
    public string    Password { get; set; } = string.Empty;
    public string    Email    { get; set; } = string.Empty;
    public AdminRole Role     { get; set; }
}
