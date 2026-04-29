using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using App.Features.AI.Data;
using App.Features.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace App.Features.Auth;

public class AuthService(IConfiguration configuration)
{
    private readonly string _jwtSecret = configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret is not configured");
    private readonly string _jwtIssuer = configuration["Jwt:Issuer"] ?? "NoMoreReceipts";
    private readonly string _jwtAudience = configuration["Jwt:Audience"] ?? "NoMoreReceiptsUsers";
    private readonly int _jwtExpiresInSeconds = int.TryParse(configuration["Jwt:ExpiresInSeconds"], out var s) ? s : 3600;

    public async Task<(bool Success, string? Error, RegisterResult? Result)> RegisterAsync(
        RegisterRequest request, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return (false, "유효한 이메일을 입력하세요.", null);

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return (false, "비밀번호는 6자 이상이어야 합니다.", null);

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return (false, "표시 이름을 입력하세요.", null);

        var exists = await db.Users.AnyAsync(u => u.Email == request.Email.ToLower());
        if (exists)
            return (false, "이미 사용 중인 이메일입니다.", null);

        var user = new User
        {
            Email = request.Email.ToLower(),
            PasswordHash = HashPassword(request.Password),
            DisplayName = request.DisplayName,
            Role = "User"
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (true, null, new RegisterResult(user.Id, user.Email, user.DisplayName, user.CreatedAt));
    }

    public async Task<(bool Success, string? Error, LoginResult? Result)> LoginAsync(
        LoginRequest request, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return (false, "이메일과 비밀번호를 입력하세요.", null);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());
        if (user is null || !VerifyPassword(request.Password, user.PasswordHash))
            return (false, "이메일 또는 비밀번호가 올바르지 않습니다.", null);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var token = GenerateJwt(user);
        return (true, null, new LoginResult(token, "Bearer", _jwtExpiresInSeconds, user.Email, user.DisplayName, user.Role));
    }

    private string GenerateJwt(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(_jwtExpiresInSeconds),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;
        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }
}