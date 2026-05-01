namespace AuthService.Models;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;       // "Admin", "Manager", "User"
    public string? Description { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
