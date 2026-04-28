using App.Features.AI.Data;
using Microsoft.EntityFrameworkCore;
namespace App.Features.Analysis
{
    /// <summary>
    /// analysis service for the analysis feature
    /// LINQ
    /// </summary>
    public class AnalysisService
    {
        private readonly AppDbContext _context;

        public AnalysisService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<CategorySpending>> GetCategorySpendingAsync()
        {
            return await _context.AiInferenceLogs
                .AsNoTracking()
                .Where(log => log.FinalCategory != null)
                .GroupBy(log => log.FinalCategory!)
                .Select(g => new CategorySpending
                {
                    Category = g.Key,
                    TotalCount = g.Count(),
                    TotalAmount = g.Count() * 5000m
                })
                .ToListAsync();
        }

        public async Task<List<MonthlyTrend>> GetMonthlyTrendAsync()
        {
            return await _context.AiInferenceLogs
                .AsNoTracking()
                .GroupBy(log => new { log.CreatedAt.Year, log.CreatedAt.Month })
                .Select(g => new MonthlyTrend
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalCount = g.Count(),
                    TotalAmount = g.Count() * 5000m
                })
                .OrderBy(t => t.Year).ThenBy(t => t.Month)
                .ToListAsync();
        }

        public async Task<AnalysisSummary> GetSummaryAsync()
        {
            var totalCount = await _context.AiInferenceLogs.CountAsync();
            var totalAmount = totalCount * 5000m;

            var topCategory = await _context.AiInferenceLogs
                .AsNoTracking()
                .Where(log => log.FinalCategory != null)
                .GroupBy(log => log.FinalCategory!)
                .Select(g => new
                {
                    Category = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .Select(x => x.Category)
                .FirstOrDefaultAsync();

            return new AnalysisSummary
            {
                TotalCount = totalCount,
                TotalAmount = totalAmount,
                TopCategory = topCategory,
                GeneratedAt = DateTimeOffset.UtcNow
            };
        }
    }
}
