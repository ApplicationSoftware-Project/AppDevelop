using App.Features.AI.Data;
using App.Features.Auth.Grpc;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace App.Features.Auth.GrpcServices;

public class UserGrpcService(AppDbContext db) : UserService.UserServiceBase
{
    public override async Task<GetUserResponse> GetUser(
        GetUserRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new GetUserResponse { Found = false };

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, context.CancellationToken);

        if (user is null)
            return new GetUserResponse { Found = false };

        return new GetUserResponse
        {
            Found       = true,
            UserId      = user.Id.ToString(),
            Email       = user.Email,
            DisplayName = user.DisplayName,
            Role        = user.Role
        };
    }

    public override async Task<CheckUserExistsResponse> CheckUserExists(
        CheckUserExistsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new CheckUserExistsResponse { Exists = false };

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Role })
            .FirstOrDefaultAsync(context.CancellationToken);

        if (user is null)
            return new CheckUserExistsResponse { Exists = false };

        return new CheckUserExistsResponse { Exists = true, Role = user.Role };
    }
}
