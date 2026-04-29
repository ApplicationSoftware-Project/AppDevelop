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
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", Register)
            .WithName("Register")
            .WithSummary("회원가입")
            .WithDescription("이메일, 비밀번호, 표시 이름으로 새 계정을 생성합니다.")
            .Accepts<RegisterRequest>("application/json")
            .Produces<RegisterResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("로그인")
            .WithDescription("이메일과 비밀번호로 JWT 액세스 토큰을 발급합니다.")
            .Accepts<LoginRequest>("application/json")
            .Produces<LoginResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", Me)
            .WithName("GetMe")
            .WithSummary("내 정보 조회")
            .WithDescription("JWT 토큰으로 인증된 사용자의 프로필을 반환합니다.")
            .Produces<MeResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }

    private static async Task<Results<Created<RegisterResult>, ValidationProblem>> Register(
        RegisterRequest request,
        AuthService authService,
        AppDbContext db,
        ILogger<Program> logger)
    {
        var (success, error, result) = await authService.RegisterAsync(request, db);

        if (!success || result is null)
        {
            logger.LogWarning("Register 실패: {Error}", error);
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["register"] = [error ?? "회원가입에 실패했습니다."]
            });
        }

        logger.LogInformation("새 사용자 등록 완료. UserId={UserId}, Email={Email}", result.UserId, result.Email);
        return TypedResults.Created($"/api/auth/me", result);
    }

    private static async Task<Results<Ok<LoginResult>, UnauthorizedHttpResult>> Login(
        LoginRequest request,
        AuthService authService,
        AppDbContext db,
        ILogger<Program> logger)
    {
        var (success, error, result) = await authService.LoginAsync(request, db);

        if (!success || result is null)
        {
            logger.LogWarning("Login 실패. Email={Email}, Reason={Reason}", request.Email, error);
            return TypedResults.Unauthorized();
        }

        logger.LogInformation("로그인 성공. Email={Email}", result.Email);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<MeResult>, UnauthorizedHttpResult>> Me(
        ClaimsPrincipal principal,
        AppDbContext db)
    {
        var userId = principal.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (userId is null || !Guid.TryParse(userId, out var guid))
            return TypedResults.Unauthorized();

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == guid);
        if (user is null)
            return TypedResults.Unauthorized();

        return TypedResults.Ok(new MeResult(user.Id, user.Email, user.DisplayName, user.Role, user.CreatedAt, user.LastLoginAt));
    }
}