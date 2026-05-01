using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using AuthService.Services;

namespace AuthService.Controllers;

/// <summary>
/// Gateway 전용: 토큰 검증 및 클레임 반환
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TokenController(IJwtService jwtService) : ControllerBase
{
    /// <summary>
    /// Gateway가 호출하는 토큰 검증 엔드포인트
    /// Authorization: Bearer {accessToken} 헤더를 검증하고 클레임을 반환합니다.
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(TokenValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Validate()
    {
        var authHeader = Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
            return Unauthorized(new { error = "Authorization 헤더가 없거나 형식이 잘못되었습니다." });

        var token = authHeader["Bearer ".Length..].Trim();
        var principal = jwtService.ValidateAccessToken(token);

        if (principal is null)
            return Unauthorized(new { error = "유효하지 않거나 만료된 토큰입니다." });

        var response = new TokenValidationResponse(
            UserId:    principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
            Email:     principal.FindFirstValue(ClaimTypes.Email) ?? "",
            Role:      principal.FindFirstValue(ClaimTypes.Role) ?? "",
            ExpiresAt: GetExpiry(principal)
        );

        return Ok(response);
    }

    private static DateTime? GetExpiry(ClaimsPrincipal principal)
    {
        var expClaim = principal.FindFirstValue("exp");
        if (expClaim is null || !long.TryParse(expClaim, out var exp)) return null;
        return DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
    }
}

public record TokenValidationResponse(
    string UserId,
    string Email,
    string Role,
    DateTime? ExpiresAt
);
