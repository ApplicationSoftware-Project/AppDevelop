using App.Features.AI.Data;
using App.Features.AI.Models;
using App.Features.Analysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

public static class AnalysisEndpoints
{
    public static void MapAnalysisEndpoints(this WebApplication app)
    {
        // /api/analysis 그룹 생성
        var group = app.MapGroup("/api/analysis")
            .WithTags("Analysis"); // Swagger에서 "Analysis" 그룹으로 표시됨

        // 1. 카테고리별 지출 합계 API
        group.MapGet("/category-total", async Task<IResult> (AnalysisService service) =>
        {
            var result = await service.GetCategorySpendingAsync();
            return TypedResults.Ok(result);
        })
        .WithName("GetCategorySpending")
        .WithSummary("카테고리별 지출 합계 조회");

        // 2. 월별 지출 추이 API
        group.MapGet("/monthly-trend", async Task<IResult> (AnalysisService service) =>
        {
            var result = await service.GetMonthlyTrendAsync();
            return TypedResults.Ok(result);
        })
        .WithName("GetMonthlyTrend")
        .WithSummary("월별 지출 추이 조회");
    }
}
