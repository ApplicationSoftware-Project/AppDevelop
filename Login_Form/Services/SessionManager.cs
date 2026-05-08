namespace App.Desktop.Services;

/// <summary>
/// 로그인 후 JWT 토큰과 사용자 정보를 앱 전역에서 보관합니다.
/// 싱글톤 패턴으로 어디서든 SessionManager.Current로 접근 가능합니다.
/// </summary>
public class SessionManager
{
    public static readonly SessionManager Current = new();

    private SessionManager() { }

    public string? AccessToken { get; private set; }
    public string? Email { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Role { get; private set; }
    public bool IsLoggedIn => AccessToken is not null;

    public void SetSession(string accessToken, string email, string displayName, string role)
    {
        AccessToken = accessToken;
        Email = email;
        DisplayName = displayName;
        Role = role;
    }

    public void Clear()
    {
        AccessToken = null;
        Email = null;
        DisplayName = null;
        Role = null;
    }
}
