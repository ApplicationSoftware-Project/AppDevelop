using App.Features.AI.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Features.AI.Services;

public sealed class AiLogQueryService
{
    public async Task<AiRecentLogsResult> GetRecentLogsAsync(int limit, AppDbContext db)
    {
        var logs = await db.AiInferenceLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .Select(x => new AiRecentLogItem(
                x.Id,
                x.ReceiptId,
                x.SuggestedCategory,
                x.Confidence,
                x.FinalCategory,
                x.IsCorrect,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync();

        return new AiRecentLogsResult(logs.Count, logs);
    }
}
