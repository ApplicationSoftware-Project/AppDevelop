namespace App.Features.Auth;

/// <summary>
/// 서비스 계층에서 반환하는 에러 코드 상수
/// 엔드포인트에서 문자열 직접 비교 대신 이 상수를 사용합니다.
/// </summary>
public static class AuthErrors
{
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string InvalidRole = "INVALID_ROLE";
}
