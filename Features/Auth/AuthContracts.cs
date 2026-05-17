namespace App.Features.Auth;

// ── 회원가입 ──────────────────────────────────────
// AdminCode: 선택값. 맞으면 Admin 계정 생성, 없으면 일반 User
public record RegisterRequest(string Email, string Password, string DisplayName, string? AdminCode = null);
public record RegisterResult(Guid UserId, string Email, string DisplayName, string Role, DateTimeOffset CreatedAt);

// ── 로그인 ────────────────────────────────────────
public record LoginRequest(string Email, string Password);
public record LoginResult(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    string Email,
    string DisplayName,
    string Role);

// ── 내 정보 ───────────────────────────────────────
public record MeResult(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    string? PhoneNumber,
    string? ProfileImageUrl,
    bool EmailNotification,
    bool PushNotification,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

// ── 프로필 수정 ───────────────────────────────────
public record UpdateProfileRequest(
    string? DisplayName,
    string? PhoneNumber,
    string? ProfileImageUrl);

// ── 비밀번호 변경 ─────────────────────────────────
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// ── 알림 설정 ─────────────────────────────────────
public record UpdateNotificationRequest(bool EmailNotification, bool PushNotification);
public record NotificationResult(bool EmailNotification, bool PushNotification);

// ── 리프레시 토큰 ─────────────────────────────────
public record RefreshTokenRequest(string RefreshToken);
public record RefreshTokenResult(string AccessToken, string RefreshToken, int ExpiresIn);

// ── Admin: 사용자 목록 ────────────────────────────
public record UserSummary(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public record UserListResult(
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    IReadOnlyList<UserSummary> Items);

// ── Admin: 사용자 상세 조회 ───────────────────────
public record AdminUserDetail(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    string? PhoneNumber,
    string? ProfileImageUrl,
    bool EmailNotification,
    bool PushNotification,
    int ReceiptCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

// ── Admin: 서비스 전체 통계 ───────────────────────
public record AdminStatsResult(
    int TotalUsers,
    int TotalAdmins,
    int TodayNewUsers,
    int TotalReceipts,
    int TodayNewReceipts,
    DateTimeOffset GeneratedAt);

// ── Admin: Role 변경 ──────────────────────────────
public record AssignRoleRequest(Guid UserId, string Role);

// ── 내부 전용 API ─────────────────────────────────
public record InternalUserInfo(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role);