using App.Features.AI.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Features.AI.Services;

public sealed class AiDashboardService
{
    public async Task<AiDashboardSummaryResult> GetSummaryAsync(int recentLimit, AppDbContext db, AiAccuracyService accuracyService, AiLogQueryService logQueryService)
    {
        var accuracy = await accuracyService.GetOverallAsync(db);
        var recentLogs = await logQueryService.GetRecentLogsAsync(recentLimit, db);
        var pendingFeedbackCount = await db.AiInferenceLogs.CountAsync(x => x.IsCorrect == null);

        return new AiDashboardSummaryResult(
            DateTimeOffset.UtcNow,
            accuracy,
            pendingFeedbackCount,
            recentLogs);
    }
}
