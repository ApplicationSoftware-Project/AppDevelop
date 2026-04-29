namespace App.Features.Auth;

public record RegisterRequest(string Email, string Password, string DisplayName);
public record RegisterResult(Guid UserId, string Email, string DisplayName, DateTimeOffset CreatedAt);

public record LoginRequest(string Email, string Password);
public record LoginResult(string AccessToken, string TokenType, int ExpiresIn, string Email, string DisplayName, string Role);

public record MeResult(Guid UserId, string Email, string DisplayName, string Role, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt);