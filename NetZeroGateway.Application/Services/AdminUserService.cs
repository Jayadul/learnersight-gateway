using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public class AdminUserService(IAdminUserRepository repo) : IAdminUserService
{
    public async Task<List<AdminUserDto>> GetAllAsync()
        => (await repo.GetAllAsync()).Select(ToDto).ToList();

    public async Task<AdminUserDto> GetByIdAsync(Guid id)
        => ToDto(await repo.GetByIdAsync(id) ?? throw new NotFoundException($"User {id} not found."));

    public async Task<AdminUserDto> CreateAsync(CreateAdminUserRequest req)
    {
        var u = new AdminUser
        {
            Username     = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Email        = req.Email,
            Role         = req.Role,
            IsActive     = true
        };
        await repo.AddAsync(u);
        await repo.SaveChangesAsync();
        return ToDto(u);
    }

    public async Task<AdminUserDto> UpdateAsync(Guid id, UpdateAdminUserRequest req)
    {
        var u = await repo.GetByIdAsync(id) ?? throw new NotFoundException($"User {id} not found.");
        u.Email    = req.Email;
        u.Role     = req.Role;
        u.IsActive = req.IsActive;
        if (!string.IsNullOrWhiteSpace(req.NewPassword))
            u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        repo.Update(u);
        await repo.SaveChangesAsync();
        return ToDto(u);
    }

    public async Task DeleteAsync(Guid id)
    {
        var u = await repo.GetByIdAsync(id) ?? throw new NotFoundException($"User {id} not found.");
        repo.Delete(u);
        await repo.SaveChangesAsync();
    }

    private static AdminUserDto ToDto(AdminUser u) => new()
    {
        Id = u.Id, Username = u.Username, Email = u.Email,
        Role = u.Role, IsActive = u.IsActive,
        LastLoginAt = u.LastLoginAt, CreatedAt = u.CreatedAt
    };
}
