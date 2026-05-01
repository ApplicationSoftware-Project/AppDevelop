namespace AuthService.Models;

// Role ↔ Permission 다대다 조인 테이블
public class RolePermission
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
