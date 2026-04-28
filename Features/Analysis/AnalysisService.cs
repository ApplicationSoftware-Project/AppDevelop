using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Features.AI.Data;
using App.Features.AI.Models;
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
    }

    public class CategorySpending
    {
        public string Category { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class MonthlyTrend
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int TotalCount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
