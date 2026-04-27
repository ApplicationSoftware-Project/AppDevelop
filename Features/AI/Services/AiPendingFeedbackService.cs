using App.Features.AI.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Features.AI.Services;

public sealed class AiPendingFeedbackService
{
    public async Task<AiPendingFeedbackResult> GetPendingAsync(int limit, AppDbContext db)
    {
        var items = await db.AiInferenceLogs
            .AsNoTracking()
            .Where(x => x.IsCorrect == null)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .Select(x => new AiPendingFeedbackItem(
                x.Id,
                x.ReceiptId,
                x.SuggestedCategory,
                x.Confidence,
                x.CreatedAt))
            .ToListAsync();

        return new AiPendingFeedbackResult(items.Count, items);
    }
}
