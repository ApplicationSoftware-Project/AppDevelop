using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthService.DTOs;
using AuthService.Services;

namespace App.AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]   // Admin Role만 접근 가능
public class AdminController(IRbacService rbacService) : ControllerBase
{
    /// <summary>전체 Role 목록 조회</summary>
    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await rbacService.GetAllRolesAsync();
        return Ok(roles.Select(r => new
        {
            r.Id,
            r.Name,
            r.Description,
            Permissions = r.RolePermissions.Select(rp => rp.Permission.Name)
        }));
    }

    /// <summary>사용자에게 Role 부여</summary>
    [HttpPost("roles/assign")]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
    {
        await rbacService.AssignRoleAsync(request);
        return Ok(new { message = $"Role '{request.RoleName}'이 부여되었습니다." });
    }

    /// <summary>사용자 Role 해제</summary>
    [HttpPost("roles/revoke")]
    public async Task<IActionResult> RevokeRole([FromBody] RevokeRoleRequest request)
    {
        await rbacService.RevokeRoleAsync(request);
        return Ok(new { message = $"Role '{request.RoleName}'이 해제되었습니다." });
    }

    /// <summary>특정 사용자의 Role + Permission 조회</summary>
    [HttpGet("users/{userId:guid}/roles")]
    public async Task<IActionResult> GetUserRoles(Guid userId)
    {
        var result = await rbacService.GetUserRolesAsync(userId);
        return Ok(result);
    }
}
