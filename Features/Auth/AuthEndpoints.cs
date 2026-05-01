using App.Features.AI.Data;
using App.Features.Auth.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace App.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");
        var admin = app.MapGroup("/api/admin").WithTags("Admin")
                       .RequireAuthorization(p => p.RequireRole("Admin"));
        var internal_ = app.MapGroup("/api/internal").WithTags("Internal");

        // ── 공개 엔드포인트 ───────────────────────────
        auth.MapPost("/register", Register)
            .WithSummary("회원가입")
            .Produces<RegisterResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        auth.MapPost("/login", Login)
            .WithSummary("로그인")
            .Produces<LoginResult>()
            .Produces(StatusCodes.Status401Unauthorized);

        auth.MapPost("/refresh", Refresh)
            .WithSummary("액세스 토큰 갱신")
            .Produces<RefreshTokenResult>()
            .Produces(StatusCodes.Status401Unauthorized);

        // ── 인증 필요 ─────────────────────────────────
        auth.MapPost("/revoke", Revoke)
            .WithSummary("로그아웃")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization();

        auth.MapGet("/me", Me)
            .WithSummary("내 정보 조회")
            .Produces<MeResult>()
            .RequireAuthorization();

        auth.MapPut("/me/profile", UpdateProfile)
            .WithSummary("프로필 수정")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization();

        auth.MapPut("/me/password", ChangePassword)
            .WithSummary("비밀번호 변경")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization();

        auth.MapGet("/me/notifications", GetNotifications)
            .WithSummary("알림 설정 조회")
            .Produces<NotificationResult>()
            .RequireAuthorization();

        auth.MapPut("/me/notifications", UpdateNotifications)
            .WithSummary("알림 설정 변경")
            .Produces<NotificationResult>()
            .RequireAuthorization();

        // ── Admin 전용 ────────────────────────────────
        admin.MapGet("/users", GetUsers)
            .WithSummary("전체 사용자 목록");

        admin.MapPost("/users/role", AssignRole)
            .WithSummary("사용자 Role 변경");

        // ── 내부 전용 API (타 서비스용) ───────────────
        internal_.MapGet("/users/{userId:guid}", GetInternalUser)
            .WithSummary("내부 사용자 정보 조회 (서비스 간 통신 전용)")
            .RequireAuthorization(p => p.RequireRole("Admin", "Service"));
    }

    // ── 핸들러 ────────────────────────────────────────

    private static async Task<Results<Created<RegisterResult>, ValidationProblem>> Register(
        RegisterRequest request, AuthService authService,
        AppDbContext db, ILogger<Program> logger)
    {
        var (success, error, result) = await authService.RegisterAsync(request, db);
        if (!success || result is null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            { ["register"] = [error ?? "회원가입에 실패했습니다."] });

        logger.LogInformation("새 사용자 등록. UserId={UserId}", result.UserId);
        return TypedResults.Created("/api/auth/me", result);
    }

    private static async Task<Results<Ok<LoginResult>, UnauthorizedHttpResult>> Login(
        LoginRequest request, AuthService authService,
        AppDbContext db, ILogger<Program> logger)
    {
        var (success, error, result) = await authService.LoginAsync(request, db);
        if (!success || result is null)
        {
            logger.LogWarning("Login 실패. Email={Email}", request.Email);
            return TypedResults.Unauthorized();
        }
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<RefreshTokenResult>, UnauthorizedHttpResult>> Refresh(
        RefreshTokenRequest request, AuthService authService, AppDbContext db)
    {
        var (success, _, result) = await authService.RefreshAsync(request, db);
        return success && result is not null
            ? TypedResults.Ok(result)
            : TypedResults.Unauthorized();
    }

    private static async Task<NoContent> Revoke(
        RefreshTokenRequest request, AuthService authService, AppDbContext db)
    {
        await authService.RevokeAsync(request.RefreshToken, db);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<MeResult>, UnauthorizedHttpResult>> Me(
        ClaimsPrincipal principal, AppDbContext db)
    {
        var user = await GetUserFromPrincipal(principal, db);
        if (user is null) return TypedResults.Unauthorized();

        return TypedResults.Ok(new MeResult(
            user.Id, user.Email, user.DisplayName, user.Role,
            user.PhoneNumber, user.ProfileImageUrl,
            user.EmailNotification, user.PushNotification,
            user.CreatedAt, user.LastLoginAt));
    }

    private static async Task<Results<NoContent, UnauthorizedHttpResult, ValidationProblem>> UpdateProfile(
        UpdateProfileRequest request, ClaimsPrincipal principal,
        AuthService authService, AppDbContext db)
    {
        var userId = GetUserId(principal);
        if (userId is null) return TypedResults.Unauthorized();

        var (success, error) = await authService.UpdateProfileAsync(userId.Value, request, db);
        if (!success)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            { ["profile"] = [error ?? "프로필 수정에 실패했습니다."] });

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, UnauthorizedHttpResult, ValidationProblem>> ChangePassword(
        ChangePasswordRequest request, ClaimsPrincipal principal,
        AuthService authService, AppDbContext db)
    {
        var userId = GetUserId(principal);
        if (userId is null) return TypedResults.Unauthorized();

        var (success, error) = await authService.ChangePasswordAsync(userId.Value, request, db);
        if (!success)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            { ["password"] = [error ?? "비밀번호 변경에 실패했습니다."] });

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<NotificationResult>, UnauthorizedHttpResult>> GetNotifications(
        ClaimsPrincipal principal, AppDbContext db)
    {
        var user = await GetUserFromPrincipal(principal, db);
        if (user is null) return TypedResults.Unauthorized();

        return TypedResults.Ok(new NotificationResult(user.EmailNotification, user.PushNotification));
    }

    private static async Task<Results<Ok<NotificationResult>, UnauthorizedHttpResult>> UpdateNotifications(
        UpdateNotificationRequest request, ClaimsPrincipal principal,
        AuthService authService, AppDbContext db)
    {
        var userId = GetUserId(principal);
        if (userId is null) return TypedResults.Unauthorized();

        var (success, result) = await authService.UpdateNotificationAsync(userId.Value, request, db);
        return success && result is not null
            ? TypedResults.Ok(result)
            : TypedResults.Unauthorized();
    }

    private static async Task<Ok<List<UserSummary>>> GetUsers(AppDbContext db)
    {
        var users = await db.Users.AsNoTracking()
            .Select(u => new UserSummary(
                u.Id, u.Email, u.DisplayName, u.Role, u.CreatedAt, u.LastLoginAt))
            .ToListAsync();
        return TypedResults.Ok(users);
    }

    private static async Task<Results<Ok<UserSummary>, NotFound>> AssignRole(
        AssignRoleRequest request, AppDbContext db)
    {
        var user = await db.Users.FindAsync(request.UserId);
        if (user is null) return TypedResults.NotFound();

        user.Role = request.Role;
        await db.SaveChangesAsync();

        return TypedResults.Ok(new UserSummary(
            user.Id, user.Email, user.DisplayName,
            user.Role, user.CreatedAt, user.LastLoginAt));
    }

    private static async Task<Results<Ok<InternalUserInfo>, NotFound>> GetInternalUser(
        Guid userId, AppDbContext db)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return TypedResults.NotFound();

        return TypedResults.Ok(new InternalUserInfo(
            user.Id, user.Email, user.DisplayName, user.Role, true));
    }

    // ── 헬퍼 ──────────────────────────────────────────
    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(
            System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(userId, out var guid) ? guid : null;
    }

    private static async Task<User?> GetUserFromPrincipal(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = GetUserId(principal);
        if (userId is null) return null;
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value);
    }
}