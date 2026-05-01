using Microsoft.EntityFrameworkCore;
using AuthService.Data;
using AuthService.DTOs;
using AuthService.Models;

namespace AuthService.Services;

public interface IRbacService
{
    Task AssignRoleAsync(AssignRoleRequest request);
    Task RevokeRoleAsync(RevokeRoleRequest request);
    Task<UserRolesResponse> GetUserRolesAsync(Guid userId);
    Task<IEnumerable<Role>> GetAllRolesAsync();
}

public class RbacService(AppDbContext db) : IRbacService
{
    // Role 부여
    public async Task AssignRoleAsync(AssignRoleRequest request)
    {
        var user = await db.Users.FindAsync(request.UserId)
            ?? throw new KeyNotFoundException("사용자를 찾을 수 없습니다.");

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == request.RoleName)
            ?? throw new KeyNotFoundException($"Role '{request.RoleName}'을 찾을 수 없습니다.");

        var exists = await db.UserRoles.AnyAsync(ur =>
            ur.UserId == request.UserId && ur.RoleId == role.Id);

        if (exists)
            throw new InvalidOperationException("이미 부여된 Role입니다.");

        db.UserRoles.Add(new UserRole { UserId = request.UserId, RoleId = role.Id });
        await db.SaveChangesAsync();
    }

    // Role 해제
    public async Task RevokeRoleAsync(RevokeRoleRequest request)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == request.RoleName)
            ?? throw new KeyNotFoundException($"Role '{request.RoleName}'을 찾을 수 없습니다.");

        var userRole = await db.UserRoles.FirstOrDefaultAsync(ur =>
            ur.UserId == request.UserId && ur.RoleId == role.Id)
            ?? throw new KeyNotFoundException("해당 사용자에게 부여된 Role이 없습니다.");

        db.UserRoles.Remove(userRole);
        await db.SaveChangesAsync();
    }

    // 유저의 Role + Permission 조회
    public async Task<UserRolesResponse> GetUserRolesAsync(Guid userId)
    {
        var user = await db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("사용자를 찾을 수 없습니다.");

        var roles = await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        var permissions = await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Name))
            .Distinct()
            .ToListAsync();

        return new UserRolesResponse(userId, user.Email, roles, permissions);
    }

    // 전체 Role 목록
    public async Task<IEnumerable<Role>> GetAllRolesAsync()
        => await db.Roles.Include(r => r.RolePermissions)
                         .ThenInclude(rp => rp.Permission)
                         .ToListAsync();
}
