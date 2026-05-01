using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using AuthService.Models;

namespace AuthService.Services;

public interface IJwtService
{
    string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateAccessToken(string token);
    ClaimsPrincipal? ParseExpiredToken(string token);
}

public class JwtService(IConfiguration config) : IJwtService
{
    private readonly string _secret   = config["Jwt:Secret"]   ?? "auth-service-default-secret-key-change-in-prod!!";
    private readonly string _issuer   = config["Jwt:Issuer"]   ?? "AuthService";
    private readonly string _audience = config["Jwt:Audience"] ?? "AuthServiceClient";
    private readonly int _accessTokenMinutes = int.Parse(config["Jwt:AccessTokenExpiryMinutes"] ?? "15");

    public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // 기본 클레임
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier,     user.Id.ToString()),
            new(ClaimTypes.Email,              user.Email),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        };

        // 다중 Role 클레임 (ClaimTypes.Role 여러 개)
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        // Permission 클레임 ("permission" 키로 여러 개)
        foreach (var perm in permissions)
            claims.Add(new Claim("permission", perm));

        var token = new JwtSecurityToken(
            issuer:            _issuer,
            audience:          _audience,
            claims:            claims,
            expires:           DateTime.UtcNow.AddMinutes(_accessTokenMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    // Gateway 인증 필터용: 만료 검증 O
    public ClaimsPrincipal? ValidateAccessToken(string token) => Validate(token, true);

    // Refresh 플로우용: 만료 검증 X
    public ClaimsPrincipal? ParseExpiredToken(string token) => Validate(token, false);

    private ClaimsPrincipal? Validate(string token, bool validateLifetime)
    {
        try
        {
            return new JwtSecurityTokenHandler().ValidateToken(token,
                new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidIssuer              = _issuer,
                    ValidateAudience         = true,
                    ValidAudience            = _audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)),
                    ValidateLifetime         = validateLifetime,
                    ClockSkew                = TimeSpan.Zero
                }, out _);
        }
        catch { return null; }
    }
}
