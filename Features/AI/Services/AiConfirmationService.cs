using App.Features.AI.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Features.AI.Services;

public sealed class AiConfirmationService
{
    public async Task<ConfirmCategoryResult?> ConfirmCategoryAsync(Guid logId, string finalCategory, AppDbContext db)
    {
        var log = await db.AiInferenceLogs.FindAsync(logId);
        if (log is null)
        {
            return null;
        }

        log.FinalCategory = finalCategory;
        log.IsCorrect = string.Equals(log.SuggestedCategory, finalCategory, StringComparison.OrdinalIgnoreCase);
        log.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync();

            return new ConfirmCategoryResult(
                log.Id,
                log.SuggestedCategory,
                log.FinalCategory,
                log.IsCorrect.GetValueOrDefault());
        }
        catch (DbUpdateException ex)
        {
            throw new AiFeatureException("AI 피드백 저장 중 오류가 발생했습니다.", StatusCodes.Status500InternalServerError, ex);
        }
    }
}
