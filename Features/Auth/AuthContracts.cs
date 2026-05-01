namespace App.Features.Auth;

// ── 회원가입 ──────────────────────────────────────
public record RegisterRequest(string Email, string Password, string DisplayName);
public record RegisterResult(Guid UserId, string Email, string DisplayName, DateTimeOffset CreatedAt);

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

// ── Admin 전용 ────────────────────────────────────
public record AssignRoleRequest(Guid UserId, string Role);
public record UserSummary(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

// ── 내부 전용 API (타 서비스용) ───────────────────
public record InternalUserInfo(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    bool IsActive);