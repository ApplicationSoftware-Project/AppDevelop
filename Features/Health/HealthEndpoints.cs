using App.Features.AI.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Features.Health;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", () => TypedResults.Ok(new
        {
            Service = "No More Receipts Gateway",
            Status = "Healthy",
            UtcNow = DateTimeOffset.UtcNow
        }))
        .WithName("Health")
        .WithSummary("게이트웨이 헬스 체크")
        .Produces(StatusCodes.Status200OK);

        app.MapGet("/api/health/ai", (IConfiguration config) =>
        {
            var hasApiKey = !string.IsNullOrWhiteSpace(config["AI:OpenAIKey"]);
            return TypedResults.Ok(new
            {
                Service = "AI Orchestrator",
                Status = hasApiKey ? "Ready" : "MissingOpenAIKey",
                UtcNow = DateTimeOffset.UtcNow
            });
        })
        .WithName("AiHealth")
        .WithSummary("AI 오케스트레이터 준비 상태 확인")
        .Produces(StatusCodes.Status200OK);

        app.MapGet("/api/health/analysis", async (AppDbContext db) =>
        {
            var totalCount = await db.AiInferenceLogs.CountAsync();
            return TypedResults.Ok(new
            {
                Service = "Analysis",
                Status = totalCount > 0 ? "Ready" : "NoData",
                TotalCount = totalCount,
                UtcNow = DateTimeOffset.UtcNow
            });
        })
        .WithName("AnalysisHealth")
        .WithSummary("Analysis 서비스 준비 상태 확인")
        .Produces(StatusCodes.Status200OK);
    }
}
