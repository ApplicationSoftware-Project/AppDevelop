using App.Features.AI.Data;
using App.Features.Auth.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;


namespace App.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");

        // [수정] Magic string 대신 RoleNames 상수 사용
        var admin = app.MapGroup("/api/admin").WithTags("Admin")
                       .RequireAuthorization(p => p.RequireRole(RoleNames.Admin));
        var internal_ = app.MapGroup("/api/internal").WithTags("Internal");

        // ── 공개 엔드포인트 ───────────────────────────
        auth.MapPost("/register", Register)
            .WithSummary("회원가입 (AdminCode 입력 시 Admin 계정 생성)")
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

        auth.MapPost("/revoke", Revoke)
            .WithSummary("로그아웃 (리프레시 토큰 무효화)")
            .Produces(StatusCodes.Status204NoContent);

        // ── 인증 필요 ─────────────────────────────────
        auth.MapGet("/me", Me)
            .WithSummary("내 정보 조회")
            .Produces<MeResult>()
            .RequireAuthorization();

        auth.MapPut("/me/profile", UpdateProfile)
            .WithSummary("프로필 수정")
            .WithDescription("PhoneNumber, ProfileImageUrl에 빈 문자열(\"\") 전송 시 해당 값이 삭제됩니다. (null 전송 시 기존 값 유지)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .RequireAuthorization();

        auth.MapPut("/me/password", ChangePassword)
            .WithSummary("비밀번호 변경")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
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
            .WithSummary("사용자 목록 조회")
            .WithDescription("""
                쿼리 파라미터:
                - page     : 페이지 번호 (기본값 1)
                - pageSize : 페이지당 항목 수 (기본값 20, 최대 100)
                - search   : 이메일 또는 표시이름 부분 검색
                - role     : 역할 필터 (User / Admin)
                """)
            .Produces<UserListResult>()
            .ProducesValidationProblem();

        admin.MapGet("/users/{userId:guid}", GetUserDetail)
            .WithSummary("특정 사용자 상세 조회")
            .Produces<AdminUserDetail>()
            .Produces(StatusCodes.Status404NotFound);

        admin.MapDelete("/users/{userId:guid}", DeleteUser)
            .WithSummary("사용자 강제 탈퇴 (영수증 포함 물리 삭제)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        admin.MapGet("/stats", GetStats)
            .WithSummary("서비스 전체 통계")
            .Produces<AdminStatsResult>();

        admin.MapPost("/users/role", AssignRole)
            .WithSummary("사용자 Role 변경 (User ↔ Admin)")
            .Produces<UserSummary>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        // ── 내부 전용 API ─────────────────────────────
        internal_.MapGet("/users/{userId:guid}", GetInternalUser)
            .WithSummary("내부 사용자 정보 조회 (서비스 간 통신 전용)")
            // [수정] Magic string 대신 RoleNames 상수 사용
            .RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Service));
    }

    // ── 핸들러 ────────────────────────────────────────

    private static async Task<Results<Created<RegisterResult>, ValidationProblem>> Register(
        RegisterRequest request, AuthService authService, AppDbContext db, ILogger<Program> logger)
    {
        var (success, error, result) = await authService.RegisterAsync(request, db);
        if (!success || result is null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            { ["register"] = [error ?? "회원가입에 실패했습니다."] });

        logger.LogInformation("새 사용자 등록. UserId={UserId}, Role={Role}", result.UserId, result.Role);
        return TypedResults.Created("/api/auth/me", result);
    }

    private static async Task<Results<Ok<LoginResult>, UnauthorizedHttpResult>> Login(
        LoginRequest request, AuthService authService, AppDbContext db, ILogger<Program> logger)
    {
        var (success, error, result) = await authService.LoginAsync(request, db);
        if (!success || result is null)
        {
            logger.LogWarning("Login 실패. Email={Email}, Reason={Reason}", request.Email, error);
            return TypedResults.Unauthorized();
        }
        logger.LogInformation("로그인 성공. Email={Email}, Role={Role}", result.Email, result.Role);
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
        UpdateProfileRequest request, ClaimsPrincipal principal, AuthService authService, AppDbContext db)
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
        ChangePasswordRequest request, ClaimsPrincipal principal, AuthService authService, AppDbContext db)
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
        UpdateNotificationRequest request, ClaimsPrincipal principal, AuthService authService, AppDbContext db)
    {
        var userId = GetUserId(principal);
        if (userId is null) return TypedResults.Unauthorized();

        var (success, result) = await authService.UpdateNotificationAsync(userId.Value, request, db);
        return success && result is not null
            ? TypedResults.Ok(result)
            : TypedResults.Unauthorized();
    }

    // ── Admin 핸들러 ──────────────────────────────────

    private static async Task<Results<Ok<UserListResult>, ValidationProblem>> GetUsers(
        int? page, int? pageSize, string? search, string? role,
        AuthService authService, AppDbContext db)
    {
        var p = page ?? 1;
        var ps = pageSize ?? 20;

        if (p < 1)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            { [nameof(page)] = ["page는 1 이상이어야 합니다."] });

        if (ps < 1 || ps > 100)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            { [nameof(pageSize)] = ["pageSize는 1~100 사이여야 합니다."] });

        // [수정] Magic string 대신 RoleNames 상수 사용
        if (role is not null && role != RoleNames.User && role != RoleNames.Admin)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            { [nameof(role)] = [$"role은 {RoleNames.User} 또는 {RoleNames.Admin} 이어야 합니다."] });

        var result = await authService.GetUsersAsync(db, p, ps, search, role);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<AdminUserDetail>, NotFound>> GetUserDetail(
        Guid userId, AuthService authService, AppDbContext db)
    {
        var detail = await authService.GetUserDetailAsync(userId, db);
        return detail is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(detail);
    }

    private static async Task<Results<NoContent, ValidationProblem, NotFound>> DeleteUser(
        Guid userId, ClaimsPrincipal principal,
        AuthService authService, AppDbContext db, ILogger<Program> logger)
    {
        var requesterId = GetUserId(principal);
        if (requesterId is null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            { ["auth"] = ["요청자 정보를 확인할 수 없습니다."] });

        if (userId == requesterId.Value)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            { [nameof(userId)] = ["자기 자신은 삭제할 수 없습니다."] });

        var deleted = await authService.DeleteUserAsync(userId, requesterId.Value, db);
        if (!deleted) return TypedResults.NotFound();

        logger.LogWarning("Admin이 사용자를 강제 탈퇴했습니다. TargetUserId={TargetUserId}, RequesterId={RequesterId}",
            userId, requesterId.Value);

        return TypedResults.NoContent();
    }

    private static async Task<Ok<AdminStatsResult>> GetStats(AuthService authService, AppDbContext db)
    {
        var stats = await authService.GetStatsAsync(db);
        return TypedResults.Ok(stats);
    }

    private static async Task<Results<Ok<UserSummary>, ValidationProblem, NotFound>> AssignRole(
        AssignRoleRequest request, AuthService authService, AppDbContext db)
    {
        var (success, error, result) = await authService.AssignRoleAsync(request, db);

        if (!success || result is null)
        {
            if (error == "사용자를 찾을 수 없습니다.")
                return TypedResults.NotFound();

            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            { ["role"] = [error ?? "Role 변경에 실패했습니다."] });
        }

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<InternalUserInfo>, NotFound>> GetInternalUser(
        Guid userId, AppDbContext db)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return TypedResults.NotFound();

        return TypedResults.Ok(new InternalUserInfo(
            user.Id, user.Email, user.DisplayName, user.Role));
    }

    // ── 헬퍼 ──────────────────────────────────────────
    // MapInboundClaims = false 설정으로 sub 클레임이 그대로 유지됨
    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(raw, out var guid) ? guid : null;
    }

    private static async Task<User?> GetUserFromPrincipal(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = GetUserId(principal);
        if (userId is null) return null;
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value);
    }
}