using App.Features.AI.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Features.Gateway;

public static class GatewayEndpoints
{
    public static void MapGatewayEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gateway").WithTags("Gateway");

        group.MapGet("/status", (IConfiguration configuration) =>
        {
            var routes = configuration
                .GetSection("ReverseProxy:Routes")
                .GetChildren()
                .Select(r => new
                {
                    RouteId   = r.Key,
                    ClusterId = r["ClusterId"],
                    Path      = r["Match:Path"]
                })
                .OrderBy(r => r.RouteId)
                .ToArray();

            var clusters = configuration
                .GetSection("ReverseProxy:Clusters")
                .GetChildren()
                .Select(c => new
                {
                    ClusterId    = c.Key,
                    Destinations = c.GetSection("Destinations")
                        .GetChildren()
                        .Select(d => d["Address"])
                        .ToArray()
                })
                .ToArray();

            return TypedResults.Ok(new
            {
                Service      = "No More Receipts Gateway",
                RouteCount   = routes.Length,
                ClusterCount = clusters.Length,
                Routes       = routes,
                Clusters     = clusters,
                UtcNow       = DateTimeOffset.UtcNow
            });
        })
        .WithSummary("YARP 게이트웨이 라우팅 설정 상태 조회")
        .Produces(StatusCodes.Status200OK);

        // 전체 서비스 상태 집계
        group.MapGet("/health", async (AppDbContext db, IConfiguration config) =>
        {
            var geminiConfigured = !string.IsNullOrWhiteSpace(config["AI:GeminiKey"]);
            var dbReachable      = false;
            var receiptCount     = 0;
            var userCount        = 0;

            try
            {
                receiptCount = await db.Receipts.CountAsync();
                userCount    = await db.Users.CountAsync();
                dbReachable  = true;
            }
            catch { }

            var services = new[]
            {
                new { Service = "Auth",     Status = dbReachable ? "Healthy" : "DatabaseError" },
                new { Service = "Receipt",  Status = dbReachable ? "Healthy" : "DatabaseError" },
                new { Service = "AI",       Status = geminiConfigured ? "Healthy" : "MissingGeminiKey" },
                new { Service = "Analysis", Status = dbReachable ? "Healthy" : "DatabaseError" }
            };

            var allHealthy = services.All(s => s.Status == "Healthy");

            return TypedResults.Ok(new
            {
                Gateway    = "No More Receipts",
                Status     = allHealthy ? "Healthy" : "Degraded",
                Services   = services,
                Stats      = new { UserCount = userCount, ReceiptCount = receiptCount },
                UtcNow     = DateTimeOffset.UtcNow
            });
        })
        .WithSummary("전체 서비스 통합 헬스 체크")
        .Produces(StatusCodes.Status200OK);

        app.MapReverseProxy();
    }
}
