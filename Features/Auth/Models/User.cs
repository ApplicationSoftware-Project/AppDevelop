namespace App.Features.Auth.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "User";

    // 프로필 관리
    public string? PhoneNumber { get; set; }
    public string? ProfileImageUrl { get; set; }

    // 알림 설정
    public bool EmailNotification { get; set; } = true;
    public bool PushNotification { get; set; } = true;

    // 리프레시 토큰
    public string? RefreshToken { get; set; }
    public DateTimeOffset? RefreshTokenExpiry { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
}