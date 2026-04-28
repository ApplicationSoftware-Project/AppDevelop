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
        .WithSummary("분석 요약 정보 조회")
        .WithDescription(AnalysisEndpointDescriptions.Summary)
        .Produces<AnalysisSummary>(StatusCodes.Status200OK);

        group.MapGet("/category-total", async Task<Ok<List<CategorySpending>>> (AnalysisService service) =>
        {
            var result = await service.GetCategorySpendingAsync();
            return TypedResults.Ok(result);
        })
        .WithName("GetCategorySpending")
        .WithSummary("카테고리별 지출 합계 조회")
        .WithDescription(AnalysisEndpointDescriptions.CategoryTotal)
        .Produces<List<CategorySpending>>(StatusCodes.Status200OK);

        group.MapGet("/monthly-trend", async Task<Ok<List<MonthlyTrend>>> (AnalysisService service) =>
        {
            var result = await service.GetMonthlyTrendAsync();
            return TypedResults.Ok(result);
        })
        .WithName("GetMonthlyTrend")
        .WithSummary("월별 지출 추이 조회")
        .WithDescription(AnalysisEndpointDescriptions.MonthlyTrend)
        .Produces<List<MonthlyTrend>>(StatusCodes.Status200OK);

        group.MapGet("/demo/checklist", GetAnalysisDemoChecklist)
            .WithName("GetAnalysisDemoChecklist")
            .WithSummary("중간발표용 분석 API 시연 체크리스트 조회")
            .WithDescription(AnalysisEndpointDescriptions.DemoChecklist)
            .Produces<AnalysisDemoChecklistResult>(StatusCodes.Status200OK);
    }

    private static Ok<AnalysisDemoChecklistResult> GetAnalysisDemoChecklist()
    {
        var steps = new List<AnalysisDemoChecklistStep>
        {
            new(
                1,
                "분석 요약 확인",
                "GET",
                "/api/analysis/summary",
                "전체 지출 개요와 최다 카테고리를 확인합니다.",
                null,
                "{ \"totalCount\": 120, \"totalAmount\": 600000, \"topCategory\": \"식비\", \"generatedAt\": \"2026-04-29T01:20:00+00:00\" }"),
            new(
                2,
                "카테고리별 합계 확인",
                "GET",
                "/api/analysis/category-total",
                "카테고리별 지출 합계를 확인합니다.",
                null,
                "[{ \"category\": \"식비\", \"totalCount\": 32, \"totalAmount\": 160000 }]"),
            new(
                3,
                "월별 추이 확인",
                "GET",
                "/api/analysis/monthly-trend",
                "월별 소비 추이를 확인합니다.",
                null,
                "[{ \"year\": 2026, \"month\": 4, \"totalCount\": 45, \"totalAmount\": 225000 }]"),
        };

        return TypedResults.Ok(new AnalysisDemoChecklistResult(steps));
    }
}
