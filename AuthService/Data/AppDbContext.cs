using Microsoft.EntityFrameworkCore;
using AuthService.Models;

namespace AuthService.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        // ── User ─────────────────────────────────────
        m.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).IsRequired().HasMaxLength(256);
            e.Property(u => u.PasswordHash).IsRequired();
        });

        // ── Role ─────────────────────────────────────
        m.Entity<Role>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.Name).IsUnique();
            e.Property(r => r.Name).IsRequired().HasMaxLength(64);
        });

        // ── Permission ───────────────────────────────
        m.Entity<Permission>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.Name).IsUnique();
            e.Property(p => p.Name).IsRequired().HasMaxLength(128);
        });

        // ── UserRole (복합 PK) ────────────────────────
        m.Entity<UserRole>(e =>
        {
            e.HasKey(ur => new { ur.UserId, ur.RoleId });
            e.HasOne(ur => ur.User)
             .WithMany(u => u.UserRoles)
             .HasForeignKey(ur => ur.UserId);
            e.HasOne(ur => ur.Role)
             .WithMany(r => r.UserRoles)
             .HasForeignKey(ur => ur.RoleId);
        });

        // ── RolePermission (복합 PK) ──────────────────
        m.Entity<RolePermission>(e =>
        {
            e.HasKey(rp => new { rp.RoleId, rp.PermissionId });
            e.HasOne(rp => rp.Role)
             .WithMany(r => r.RolePermissions)
             .HasForeignKey(rp => rp.RoleId);
            e.HasOne(rp => rp.Permission)
             .WithMany(p => p.RolePermissions)
             .HasForeignKey(rp => rp.PermissionId);
        });

        // ── 시드 데이터 ───────────────────────────────
        m.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Admin",   Description = "모든 권한" },
            new Role { Id = 2, Name = "Manager", Description = "조회 및 수정 권한" },
            new Role { Id = 3, Name = "User",    Description = "기본 사용자 권한" }
        );

        m.Entity<Permission>().HasData(
            new Permission { Id = 1,  Name = "users:read",    Description = "사용자 조회" },
            new Permission { Id = 2,  Name = "users:write",   Description = "사용자 생성/수정" },
            new Permission { Id = 3,  Name = "users:delete",  Description = "사용자 삭제" },
            new Permission { Id = 4,  Name = "orders:read",   Description = "주문 조회" },
            new Permission { Id = 5,  Name = "orders:write",  Description = "주문 생성/수정" },
            new Permission { Id = 6,  Name = "orders:delete", Description = "주문 삭제" },
            new Permission { Id = 7,  Name = "reports:read",  Description = "리포트 조회" }
        );

        // Admin → 전체 권한
        m.Entity<RolePermission>().HasData(
            new RolePermission { RoleId = 1, PermissionId = 1 },
            new RolePermission { RoleId = 1, PermissionId = 2 },
            new RolePermission { RoleId = 1, PermissionId = 3 },
            new RolePermission { RoleId = 1, PermissionId = 4 },
            new RolePermission { RoleId = 1, PermissionId = 5 },
            new RolePermission { RoleId = 1, PermissionId = 6 },
            new RolePermission { RoleId = 1, PermissionId = 7 }
        );

        // Manager → 조회 + 주문 수정
        m.Entity<RolePermission>().HasData(
            new RolePermission { RoleId = 2, PermissionId = 1 },
            new RolePermission { RoleId = 2, PermissionId = 4 },
            new RolePermission { RoleId = 2, PermissionId = 5 },
            new RolePermission { RoleId = 2, PermissionId = 7 }
        );

        // User → 본인 조회 + 주문 조회만
        m.Entity<RolePermission>().HasData(
            new RolePermission { RoleId = 3, PermissionId = 1 },
            new RolePermission { RoleId = 3, PermissionId = 4 }
        );
    }
}
