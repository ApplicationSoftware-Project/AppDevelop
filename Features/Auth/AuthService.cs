using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using App.Features.AI.Data;
using App.Features.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace App.Features.Auth;

public class AuthService(IConfiguration configuration)
{
    private readonly string _jwtSecret = configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret is not configured");
    private readonly string _jwtIssuer = configuration["Jwt:Issuer"] ?? "NoMoreReceipts";
    private readonly string _jwtAudience = configuration["Jwt:Audience"] ?? "NoMoreReceiptsUsers";
    private readonly int _jwtExpiresInSeconds =
        int.TryParse(configuration["Jwt:ExpiresInSeconds"], out var s) ? s : 3600;
    private readonly int _refreshTokenDays =
        int.TryParse(configuration["Jwt:RefreshTokenExpiryDays"], out var d) ? d : 7;

    // ── 회원가입 ─────────────────────────────────────
    public async Task<(bool Success, string? Error, RegisterResult? Result)> RegisterAsync(
        RegisterRequest request, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return (false, "유효한 이메일을 입력하세요.", null);
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return (false, "비밀번호는 6자 이상이어야 합니다.", null);
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return (false, "표시 이름을 입력하세요.", null);
        if (await db.Users.AnyAsync(u => u.Email == request.Email.ToLower()))
            return (false, "이미 사용 중인 이메일입니다.", null);

        var user = new User
        {
            Email = request.Email.ToLower(),
            PasswordHash = HashPassword(request.Password),
            DisplayName = request.DisplayName,
            Role = "User"
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (true, null, new RegisterResult(user.Id, user.Email, user.DisplayName, user.CreatedAt));
    }

    // ── 로그인 ───────────────────────────────────────
    public async Task<(bool Success, string? Error, LoginResult? Result)> LoginAsync(
        LoginRequest request, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return (false, "이메일과 비밀번호를 입력하세요.", null);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());
        if (user is null || !VerifyPassword(request.Password, user.PasswordHash))
            return (false, "이메일 또는 비밀번호가 올바르지 않습니다.", null);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        user.RefreshToken = GenerateRefreshToken();
        user.RefreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(_refreshTokenDays);
        await db.SaveChangesAsync();

        return (true, null, new LoginResult(
            GenerateJwt(user), user.RefreshToken, "Bearer",
            _jwtExpiresInSeconds, user.Email, user.DisplayName, user.Role));
    }

    // ── 리프레시 토큰 ────────────────────────────────
    public async Task<(bool Success, string? Error, RefreshTokenResult? Result)> RefreshAsync(
        RefreshTokenRequest request, AppDbContext db)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);

        // [보안 수정 4] RefreshTokenExpiry null 체크 추가
        if (user is null || user.RefreshTokenExpiry is null || user.RefreshTokenExpiry < DateTimeOffset.UtcNow)
            return (false, "유효하지 않거나 만료된 리프레시 토큰입니다.", null);

        user.RefreshToken = GenerateRefreshToken();
        user.RefreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(_refreshTokenDays);
        await db.SaveChangesAsync();

        return (true, null, new RefreshTokenResult(
            GenerateJwt(user), user.RefreshToken!, _jwtExpiresInSeconds));
    }

    // ── 로그아웃 ─────────────────────────────────────
    public async Task RevokeAsync(string refreshToken, AppDbContext db)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
        if (user is null) return;
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await db.SaveChangesAsync();
    }

    // ── 프로필 수정 ──────────────────────────────────
    public async Task<(bool Success, string? Error)> UpdateProfileAsync(
        Guid userId, UpdateProfileRequest request, AppDbContext db)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return (false, "사용자를 찾을 수 없습니다.");

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
            user.DisplayName = request.DisplayName;
        if (request.PhoneNumber is not null)
            user.PhoneNumber = request.PhoneNumber;
        if (request.ProfileImageUrl is not null)
            user.ProfileImageUrl = request.ProfileImageUrl;

        await db.SaveChangesAsync();
        return (true, null);
    }

    // ── 비밀번호 변경 ────────────────────────────────
    public async Task<(bool Success, string? Error)> ChangePasswordAsync(
        Guid userId, ChangePasswordRequest request, AppDbContext db)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return (false, "사용자를 찾을 수 없습니다.");
        if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
            return (false, "현재 비밀번호가 올바르지 않습니다.");
        if (request.NewPassword.Length < 6)
            return (false, "새 비밀번호는 6자 이상이어야 합니다.");

        user.PasswordHash = HashPassword(request.NewPassword);
        // [보안 수정 3] 비밀번호 변경 시 리프레시 토큰 폐기
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await db.SaveChangesAsync();
        return (true, null);
    }

    // ── 알림 설정 변경 ───────────────────────────────
    public async Task<(bool Success, NotificationResult? Result)> UpdateNotificationAsync(
        Guid userId, UpdateNotificationRequest request, AppDbContext db)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return (false, null);

        user.EmailNotification = request.EmailNotification;
        user.PushNotification = request.PushNotification;
        await db.SaveChangesAsync();

        return (true, new NotificationResult(user.EmailNotification, user.PushNotification));
    }

    // ── JWT 생성 ─────────────────────────────────────
    private string GenerateJwt(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name,               user.DisplayName),
            new Claim(ClaimTypes.Role,               user.Role),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(_jwtExpiresInSeconds),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    // ── PBKDF2 (SHA-256, 100,000회) ──────────────────
    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;
        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }
}