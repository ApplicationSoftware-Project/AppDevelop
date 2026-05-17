using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using App.Features.AI.Data;
using App.Features.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace App.Features.Auth;

public class AuthService(IConfiguration configuration, ILogger<AuthService> logger)
{
    private readonly string _jwtSecret = configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret is not configured");
    private readonly string _jwtIssuer = configuration["Jwt:Issuer"] ?? "NoMoreReceipts";
    private readonly string _jwtAudience = configuration["Jwt:Audience"] ?? "NoMoreReceiptsUsers";
    private readonly int _jwtExpiresInSeconds =
        int.TryParse(configuration["Jwt:ExpiresInSeconds"], out var s) ? s : 3600;
    private readonly int _refreshTokenDays =
        int.TryParse(configuration["Jwt:RefreshTokenExpiryDays"], out var d) ? d : 7;
    private readonly string? _adminCode = configuration["Seed:AdminCode"];

    // ── 회원가입 ─────────────────────────────────────
    public async Task<(bool Success, string? Error, RegisterResult? Result)> RegisterAsync(
        RegisterRequest request, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return (false, "이메일을 입력하세요.", null);

        var email = request.Email.Trim().ToLower();
        var displayName = request.DisplayName?.Trim() ?? string.Empty;

        if (email.Length > 256)
            return (false, "이메일은 256자 이하여야 합니다.", null);
        if (!IsValidEmail(email))
            return (false, "유효한 이메일 형식을 입력하세요.", null);
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return (false, "비밀번호는 6자 이상이어야 합니다.", null);
        if (string.IsNullOrWhiteSpace(displayName))
            return (false, "표시 이름을 입력하세요.", null);
        if (displayName.Length > 100)
            return (false, "표시 이름은 100자 이하여야 합니다.", null);

        if (await db.Users.AnyAsync(u => u.Email == email))
            return (false, "이미 사용 중인 이메일입니다.", null);

        var role = RoleNames.User;
        if (!string.IsNullOrWhiteSpace(request.AdminCode))
        {
            if (string.IsNullOrWhiteSpace(_adminCode))
                return (false, "관리자 코드가 서버에 설정되어 있지 않습니다.", null);
            if (request.AdminCode.Trim() != _adminCode.Trim())
                return (false, "관리자 코드가 올바르지 않습니다.", null);
            role = RoleNames.Admin;
        }

        var user = new User
        {
            Email = email,
            PasswordHash = HashPassword(request.Password),
            DisplayName = displayName,
            Role = role
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        logger.LogInformation("회원가입 완료. UserId={UserId}, Role={Role}", user.Id, user.Role);

        return (true, null, new RegisterResult(
            user.Id, user.Email, user.DisplayName, user.Role, user.CreatedAt));
    }

    // ── 로그인 ───────────────────────────────────────
    public async Task<(bool Success, string? Error, LoginResult? Result)> LoginAsync(
        LoginRequest request, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return (false, "이메일과 비밀번호를 입력하세요.", null);

        var email = request.Email.Trim().ToLower();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
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
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return (false, "토큰이 제공되지 않았습니다.", null);

        var user = await db.Users.FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);

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
        if (string.IsNullOrWhiteSpace(refreshToken)) return;

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
        {
            var trimmed = request.DisplayName.Trim();
            if (trimmed.Length > 100)
                return (false, "표시 이름은 100자 이하여야 합니다.");
            user.DisplayName = trimmed;
        }

        // null → 수정 안 함, 빈 문자열("") → null로 저장(삭제)
        if (request.PhoneNumber is not null)
            user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
                ? null
                : request.PhoneNumber.Trim();

        if (request.ProfileImageUrl is not null)
            user.ProfileImageUrl = string.IsNullOrWhiteSpace(request.ProfileImageUrl)
                ? null
                : request.ProfileImageUrl.Trim();

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
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await db.SaveChangesAsync();

        logger.LogInformation("비밀번호 변경 완료. UserId={UserId}", userId);

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

    // ── Admin: 사용자 목록 ───────────────────────────
    public async Task<UserListResult> GetUsersAsync(
        AppDbContext db, int page, int pageSize, string? search, string? role)
    {
        var query = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(u =>
                u.Email.Contains(keyword) ||
                u.DisplayName.ToLower().Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.Role == role);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserSummary(
                u.Id, u.Email, u.DisplayName, u.Role, u.CreatedAt, u.LastLoginAt))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling((double)total / pageSize);
        return new UserListResult(total, page, pageSize, totalPages, items);
    }

    // ── Admin: 사용자 상세 조회 ──────────────────────
    public async Task<AdminUserDetail?> GetUserDetailAsync(Guid userId, AppDbContext db)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return null;

        var receiptCount = await db.Receipts.CountAsync(r => r.UserId == userId);

        return new AdminUserDetail(
            user.Id, user.Email, user.DisplayName, user.Role,
            user.PhoneNumber, user.ProfileImageUrl,
            user.EmailNotification, user.PushNotification,
            receiptCount, user.CreatedAt, user.LastLoginAt);
    }

    // ── Admin: Role 변경 ──────────────────────────────
    public async Task<(bool Success, string? Error, UserSummary? Result)> AssignRoleAsync(
        AssignRoleRequest request, AppDbContext db)
    {
        var allowed = new[] { RoleNames.User, RoleNames.Admin };
        if (!allowed.Contains(request.Role))
            return (false, AuthErrors.InvalidRole, null);

        var user = await db.Users.FindAsync(request.UserId);
        if (user is null) return (false, AuthErrors.UserNotFound, null);

        var prevRole = user.Role;
        user.Role = request.Role;
        await db.SaveChangesAsync();

        logger.LogInformation("Role 변경. UserId={UserId}, {PrevRole} → {NewRole}",
            request.UserId, prevRole, request.Role);

        return (true, null, new UserSummary(
            user.Id, user.Email, user.DisplayName, user.Role, user.CreatedAt, user.LastLoginAt));
    }

    // ── Admin: 사용자 강제 탈퇴 ──────────────────────
    public async Task<bool> DeleteUserAsync(Guid userId, Guid requesterId, AppDbContext db)
    {
        if (userId == requesterId) return false;

        var user = await db.Users.FindAsync(userId);
        if (user is null) return false;

        using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await db.Receipts.Where(r => r.UserId == userId).ExecuteDeleteAsync();
            db.Users.Remove(user);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            logger.LogWarning("사용자 강제 탈퇴. TargetUserId={TargetUserId}, RequesterId={RequesterId}",
                userId, requesterId);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "사용자 강제 탈퇴 실패. TargetUserId={TargetUserId}", userId);
            return false;
        }
    }

    // ── Admin: 서비스 전체 통계 ───────────────────────
    public async Task<AdminStatsResult> GetStatsAsync(AppDbContext db)
    {
        var todayUtc = DateTimeOffset.UtcNow.Date;

        return new AdminStatsResult(
            await db.Users.CountAsync(),
            await db.Users.CountAsync(u => u.Role == RoleNames.Admin),
            await db.Users.CountAsync(u => u.CreatedAt >= todayUtc),
            await db.Receipts.CountAsync(),
            await db.Receipts.CountAsync(r => r.CreatedAt >= todayUtc),
            DateTimeOffset.UtcNow);
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

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            var addr = new MailAddress(email);
            return addr.Address.Equals(email, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

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