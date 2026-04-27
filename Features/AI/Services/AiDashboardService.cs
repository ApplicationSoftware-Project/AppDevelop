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
        var hasInferenceData = accuracy.TotalCount > 0;
        var statusMessage = hasInferenceData
            ? "AI 추론 데이터가 존재합니다. 최근 로그와 정확도 지표를 확인하세요."
            : "아직 AI 추론 데이터가 없습니다. suggest-category 호출 후 다시 확인하세요.";

        return new AiDashboardSummaryResult(
            DateTimeOffset.UtcNow,
            statusMessage,
            hasInferenceData,
            accuracy,
            pendingFeedbackCount,
            recentLogs);
    }
}
