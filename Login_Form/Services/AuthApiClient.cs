using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace App.Desktop.Services;

// 백엔드 AuthContracts.cs의 record들과 동일한 구조
public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string DisplayName);

public record LoginResult(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string Email,
    string DisplayName,
    string Role);

public record RegisterResult(
    Guid UserId,
    string Email,
    string DisplayName,
    DateTimeOffset CreatedAt);

public record ApiError(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("errors")] Dictionary<string, string[]>? Errors);

/// <summary>
/// 백엔드 /api/auth 엔드포인트를 호출합니다.
/// BaseAddress는 appsettings 또는 Program.cs에서 주입됩니다.
/// </summary>
public class AuthApiClient
{
    private readonly HttpClient _http;

    public AuthApiClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// POST /api/auth/login
    /// 성공: (true, null, LoginResult)
    /// 실패: (false, 에러메시지, null)
    /// </summary>
    public async Task<(bool Success, string? Error, LoginResult? Result)> LoginAsync(
        string email, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResult>();
                return (true, null, result);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (false, "이메일 또는 비밀번호가 올바르지 않습니다.", null);

            return (false, $"서버 오류가 발생했습니다. ({(int)response.StatusCode})", null);
        }
        catch (HttpRequestException)
        {
            return (false, "서버에 연결할 수 없습니다. 백엔드가 실행 중인지 확인하세요.", null);
        }
        catch (TaskCanceledException)
        {
            return (false, "요청 시간이 초과되었습니다.", null);
        }
    }

    /// <summary>
    /// POST /api/auth/register
    /// 성공: (true, null, RegisterResult)
    /// 실패: (false, 에러메시지, null)
    /// </summary>
    public async Task<(bool Success, string? Error, RegisterResult? Result)> RegisterAsync(
        string email, string password, string displayName)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(email, password, displayName));

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RegisterResult>();
                return (true, null, result);
            }

            // 백엔드가 ValidationProblem으로 에러 반환
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            var message = error?.Errors?.Values.FirstOrDefault()?.FirstOrDefault()
                ?? "회원가입에 실패했습니다.";
            return (false, message, null);
        }
        catch (HttpRequestException)
        {
            return (false, "서버에 연결할 수 없습니다. 백엔드가 실행 중인지 확인하세요.", null);
        }
        catch (TaskCanceledException)
        {
            return (false, "요청 시간이 초과되었습니다.", null);
        }
    }
}
