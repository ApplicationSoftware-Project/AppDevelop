namespace AuthService.Models;

public class Permission
{
    public int Id { get; set; }

    // "users:read", "users:write", "orders:delete" 형식
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
