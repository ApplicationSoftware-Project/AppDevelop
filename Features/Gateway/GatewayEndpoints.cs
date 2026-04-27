namespace App.Features.Gateway;

public static class GatewayEndpoints
{
    public static void MapGatewayEndpoints(this WebApplication app)
    {
        app.MapGet("/api/gateway/status", (IConfiguration configuration) =>
        {
            var routes = configuration
                .GetSection("ReverseProxy:Routes")
                .GetChildren()
                .Select(x => x.Key)
                .OrderBy(x => x)
                .ToArray();

            var clusters = configuration
                .GetSection("ReverseProxy:Clusters")
                .GetChildren()
                .Select(x => x.Key)
                .OrderBy(x => x)
                .ToArray();

            return TypedResults.Ok(new
            {
                Service = "No More Receipts Gateway",
                HasRouteConfig = routes.Length > 0,
                HasClusterConfig = clusters.Length > 0,
                RouteCount = routes.Length,
                ClusterCount = clusters.Length,
                Routes = routes,
                Clusters = clusters,
                UtcNow = DateTimeOffset.UtcNow
            });
        })
        .WithName("GatewayStatus")
        .WithSummary("YARP 게이트웨이 라우팅 설정 상태 조회")
        .Produces(StatusCodes.Status200OK);

        app.MapReverseProxy();
    }
}
