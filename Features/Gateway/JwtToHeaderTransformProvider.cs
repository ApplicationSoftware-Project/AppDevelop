using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace App.Features.Gateway;

// Gateway에서 JWT를 파싱해 downstream 서비스에 X-User-Id, X-User-Role 헤더를 추가
// downstream 서비스는 DB 조회 없이 헤더만 읽어 사용자 정보를 활용할 수 있음
public class JwtToHeaderTransformProvider : ITransformProvider
{
    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(transformContext =>
        {
            var principal = transformContext.HttpContext.User;

            if (principal.Identity?.IsAuthenticated != true)
                return ValueTask.CompletedTask;

            var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var role   = principal.FindFirstValue(ClaimTypes.Role);

            if (userId is not null)
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Id", userId);
            if (role is not null)
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Role", role);

            return ValueTask.CompletedTask;
        });
    }

    public void ValidateCluster(TransformClusterValidationContext context) { }
    public void ValidateRoute(TransformRouteValidationContext context) { }
}
