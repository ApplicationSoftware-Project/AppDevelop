using Microsoft.EntityFrameworkCore;
using AuthService.Data;
using AuthService.DTOs;
using AuthService.Models;

namespace AuthService.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request);
    Task RevokeAsync(string refreshToken);
}

public class AuthService(AppDbContext db, IJwtService jwtService, IConfiguration config) : IAuthService
{
    private readonly int _refreshDays = int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "7");

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (await db.Users.AnyAsync(u => u.Email == request.Email))
            throw new InvalidOperationException("이미 사용 중인 이메일입니다.");

        var user = new User
        {
            Email        = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        };

        // 신규 가입자는 기본 "User" Role 부여
        var userRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "User")
            ?? throw new InvalidOperationException("기본 Role이 존재하지 않습니다. 마이그레이션을 확인하세요.");

        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });
        await db.SaveChangesAsync();

        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email)
            ?? throw new UnauthorizedAccessException("이메일 또는 비밀번호가 올바르지 않습니다.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("이메일 또는 비밀번호가 올바르지 않습니다.");

        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.RefreshToken == request.RefreshToken)
            ?? throw new UnauthorizedAccessException("유효하지 않은 리프레시 토큰입니다.");

        if (user.RefreshTokenExpiry < DateTime.UtcNow)
            throw new UnauthorizedAccessException("리프레시 토큰이 만료되었습니다.");

        return await IssueTokensAsync(user);
    }

    public async Task RevokeAsync(string refreshToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.RefreshToken == refreshToken);
        if (user is null) return;
        user.RefreshToken      = null;
        user.RefreshTokenExpiry = null;
        await db.SaveChangesAsync();
    }

    // ── 공통: Role/Permission 로드 후 토큰 발급 ──────
    private async Task<AuthResponse> IssueTokensAsync(User user)
    {
        // 유저의 Role 목록 조회
        var roles = await db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        // Role에 연결된 Permission 목록 조회 (중복 제거)
        var permissions = await db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Include(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Name))
            .Distinct()
            .ToListAsync();

        var accessToken  = jwtService.GenerateAccessToken(user, roles, permissions);
        var refreshToken = jwtService.GenerateRefreshToken();
        var expiry       = DateTime.UtcNow.AddMinutes(int.Parse(config["Jwt:AccessTokenExpiryMinutes"] ?? "15"));

        user.RefreshToken       = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_refreshDays);
        await db.SaveChangesAsync();

        return new AuthResponse(accessToken, refreshToken, expiry, user.Email, roles, permissions);
    }
}
