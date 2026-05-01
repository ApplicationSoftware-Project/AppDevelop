namespace AuthService.DTOs;

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    string Email,
    IEnumerable<string> Roles,
    IEnumerable<string> Permissions
);

public record RefreshTokenRequest(string RefreshToken);
public record MessageResponse(string Message);

// 관리자용 Role 관리 DTO
public record AssignRoleRequest(Guid UserId, string RoleName);
public record RevokeRoleRequest(Guid UserId, string RoleName);
public record UserRolesResponse(Guid UserId, string Email, IEnumerable<string> Roles, IEnumerable<string> Permissions);
