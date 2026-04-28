using Microsoft.AspNetCore.Http.HttpResults;

namespace App.Features.Analysis;

public static class AnalysisEndpoints
{
    public static void MapAnalysisEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analysis")
            .WithTags("Analysis");

        group.MapGet("/summary", async Task<Ok<AnalysisSummary>> (AnalysisService service) =>
        {
            var result = await service.GetSummaryAsync();
            return TypedResults.Ok(result);
        })
        .WithName("GetAnalysisSummary")
        .WithSummary("분석 요약 정보 조회");

        group.MapGet("/category-total", async Task<Ok<List<CategorySpending>>> (AnalysisService service) =>
        {
            var result = await service.GetCategorySpendingAsync();
            return TypedResults.Ok(result);
        })
        .WithName("GetCategorySpending")
        .WithSummary("카테고리별 지출 합계 조회");

        group.MapGet("/monthly-trend", async Task<Ok<List<MonthlyTrend>>> (AnalysisService service) =>
        {
            var result = await service.GetMonthlyTrendAsync();
            return TypedResults.Ok(result);
        })
        .WithName("GetMonthlyTrend")
        .WithSummary("월별 지출 추이 조회");
    }
}
