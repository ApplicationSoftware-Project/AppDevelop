using App.Features.AI.Data;
using App.Features.AI.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.SemanticKernel;

namespace App.Features.AI;

public static class AiEndpoints
{
    public static void MapAiEndpoints(this WebApplication app)
    {
        app.MapPost("/api/ai/suggest-category", SuggestCategory)
            .WithName("SuggestCategory")
            .WithSummary("영수증 OCR 텍스트 기반 AI 카테고리 추천")
            .WithDescription(AiEndpointDescriptions.SuggestCategory)
            .Accepts<SuggestCategoryRequest>("application/json")
            .Produces<SuggestCategoryResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        app.MapPost("/api/ai/confirm-category", ConfirmCategory)
            .WithName("ConfirmCategory")
            .WithSummary("AI 추천 카테고리 사용자 확정")
            .WithDescription(AiEndpointDescriptions.ConfirmCategory)
            .Accepts<ConfirmCategoryRequest>("application/json")
            .Produces<ConfirmCategoryResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/ai/accuracy", GetAiAccuracy)
            .WithName("GetAiAccuracy")
            .WithSummary("AI 카테고리 추천 정확도 조회")
            .WithDescription(AiEndpointDescriptions.Accuracy)
            .Produces<AiAccuracyResult>(StatusCodes.Status200OK);

        app.MapGet("/api/ai/accuracy/daily", GetAiAccuracyDaily)
            .WithName("GetAiAccuracyDaily")
            .WithSummary("AI 일별 카테고리 추천 정확도 조회")
            .WithDescription(AiEndpointDescriptions.AccuracyDaily)
            .Produces<AiAccuracyDailyResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/ai/accuracy/weekly", GetAiAccuracyWeekly)
            .WithName("GetAiAccuracyWeekly")
            .WithSummary("AI 주별 카테고리 추천 정확도 조회")
            .WithDescription(AiEndpointDescriptions.AccuracyWeekly)
            .Produces<AiAccuracyWeeklyResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/ai/accuracy/monthly", GetAiAccuracyMonthly)
            .WithName("GetAiAccuracyMonthly")
            .WithSummary("AI 월별 카테고리 추천 정확도 조회")
            .WithDescription(AiEndpointDescriptions.AccuracyMonthly)
            .Produces<AiAccuracyMonthlyResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/ai/logs/recent", GetAiRecentLogs)
            .WithName("GetAiRecentLogs")
            .WithSummary("최근 AI 추론 로그 조회")
            .WithDescription(AiEndpointDescriptions.RecentLogs)
            .Produces<AiRecentLogsResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/ai/dashboard/summary", GetAiDashboardSummary)
            .WithName("GetAiDashboardSummary")
            .WithSummary("AI 대시보드 요약 조회")
            .WithDescription(AiEndpointDescriptions.DashboardSummary)
            .Produces<AiDashboardSummaryResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/ai/demo/checklist", GetAiDemoChecklist)
            .WithName("GetAiDemoChecklist")
            .WithSummary("중간발표용 AI API 시연 체크리스트 조회")
            .WithDescription(AiEndpointDescriptions.DemoChecklist)
            .Produces<AiDemoChecklistResult>(StatusCodes.Status200OK);
    }

    private static async Task<Results<Ok<SuggestCategoryResult>, ValidationProblem, ProblemHttpResult>> SuggestCategory(
        SuggestCategoryRequest request,
        Kernel k,
        AppDbContext db,
        AiSuggestionService suggestionService,
        ILogger<Program> logger)
    {
        var suggestValidation = AiValidationService.ValidateSuggestCategoryRequest(request);
        if (suggestValidation is not null)
        {
            logger.LogWarning("suggest-category 요청 거부: Validation 실패");
            return TypedResults.ValidationProblem(suggestValidation);
        }

        try
        {
            var result = await suggestionService.SuggestCategoryAsync(request, k, db);
            logger.LogInformation("AI 추천 로그 저장 완료. ReceiptId={ReceiptId}, Category={Category}, Confidence={Confidence}",
                request.ReceiptId, result.Category, result.Confidence);
            return TypedResults.Ok(result);
        }
        catch (AiFeatureException ex)
        {
            logger.LogWarning("suggest-category 처리 실패. ReceiptId={ReceiptId}, Detail={Detail}", request.ReceiptId, ex.Message);
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: ex.StatusCode);
        }
    }

    private static async Task<Results<Ok<ConfirmCategoryResult>, ValidationProblem, NotFound, ProblemHttpResult>> ConfirmCategory(
        ConfirmCategoryRequest request,
        AppDbContext db,
        AiConfirmationService confirmationService,
        ILogger<Program> logger)
    {
        if (request.LogId == Guid.Empty)
        {
            logger.LogWarning("confirm-category 요청 거부: logId가 비어 있음");
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.LogId)] = ["logId는 비어 있을 수 없습니다."]
            });
        }

        if (string.IsNullOrWhiteSpace(request.FinalCategory))
        {
            logger.LogWarning("confirm-category 요청 거부: finalCategory가 비어 있음");
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.FinalCategory)] = ["finalCategory는 비어 있을 수 없습니다."]
            });
        }

        var validation = AiValidationService.ValidateAndNormalizeConfirmCategoryRequest(request);
        if (validation.Errors is not null)
        {
            logger.LogWarning("confirm-category 요청 거부: Validation 실패");
            return TypedResults.ValidationProblem(validation.Errors);
        }

        var finalCategory = validation.NormalizedFinalCategory!;

        try
        {
            var result = await confirmationService.ConfirmCategoryAsync(request.LogId, finalCategory, db);
            if (result is null)
            {
                logger.LogWarning("confirm-category 대상 로그 없음. LogId={LogId}", request.LogId);
                return TypedResults.NotFound();
            }

            logger.LogInformation("AI 피드백 저장 완료. LogId={LogId}, Suggested={SuggestedCategory}, Final={FinalCategory}, IsCorrect={IsCorrect}",
                result.LogId, result.SuggestedCategory, result.FinalCategory, result.IsCorrect);

            return TypedResults.Ok(result);
        }
        catch (AiFeatureException ex)
        {
            logger.LogError("confirm-category 처리 실패. LogId={LogId}, Detail={Detail}", request.LogId, ex.Message);
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: ex.StatusCode);
        }
    }

    private static async Task<Ok<AiAccuracyResult>> GetAiAccuracy(AppDbContext db, AiAccuracyService accuracyService)
    {
        var result = await accuracyService.GetOverallAsync(db);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<AiAccuracyDailyResult>, ValidationProblem>> GetAiAccuracyDaily(int days, AppDbContext db, AiAccuracyService accuracyService)
    {
        if (days < 1 || days > 365)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(days)] = ["days는 1 이상 365 이하여야 합니다."]
            });
        }

        var result = await accuracyService.GetDailyAsync(days, db);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<AiAccuracyWeeklyResult>, ValidationProblem>> GetAiAccuracyWeekly(int weeks, AppDbContext db, AiAccuracyService accuracyService)
    {
        if (weeks < 1 || weeks > 104)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(weeks)] = ["weeks는 1 이상 104 이하여야 합니다."]
            });
        }

        var result = await accuracyService.GetWeeklyAsync(weeks, db);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<AiAccuracyMonthlyResult>, ValidationProblem>> GetAiAccuracyMonthly(int months, AppDbContext db, AiAccuracyService accuracyService)
    {
        if (months < 1 || months > 36)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(months)] = ["months는 1 이상 36 이하여야 합니다."]
            });
        }

        var result = await accuracyService.GetMonthlyAsync(months, db);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<AiRecentLogsResult>, ValidationProblem>> GetAiRecentLogs(int? limit, AppDbContext db, AiLogQueryService logQueryService)
    {
        var effectiveLimit = limit ?? 20;

        if (effectiveLimit < 1 || effectiveLimit > 200)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(limit)] = ["limit은 1 이상 200 이하여야 합니다."]
            });
        }

        var result = await logQueryService.GetRecentLogsAsync(effectiveLimit, db);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<AiDashboardSummaryResult>, ValidationProblem>> GetAiDashboardSummary(
        int? recentLimit,
        AppDbContext db,
        AiAccuracyService accuracyService,
        AiLogQueryService logQueryService,
        AiDashboardService dashboardService)
    {
        var effectiveRecentLimit = recentLimit ?? 10;

        if (effectiveRecentLimit < 1 || effectiveRecentLimit > 50)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(recentLimit)] = ["recentLimit은 1 이상 50 이하여야 합니다."]
            });
        }

        var result = await dashboardService.GetSummaryAsync(effectiveRecentLimit, db, accuracyService, logQueryService);
        return TypedResults.Ok(result);
    }

    private static Ok<AiDemoChecklistResult> GetAiDemoChecklist()
    {
        var steps = new List<AiDemoChecklistStep>
        {
            new(
                1,
                "Gateway/AI 상태 점검",
                "GET",
                "/api/health/ai",
                "AI 오케스트레이터 준비 상태를 확인합니다.",
                null,
                "{ \"service\": \"AI Orchestrator\", \"status\": \"Ready\", \"utcNow\": \"2026-01-10T01:40:00+00:00\" }"),
            new(
                2,
                "AI 카테고리 추천",
                "POST",
                "/api/ai/suggest-category",
                "OCR 텍스트 기반으로 AI 추천 카테고리를 생성하고 로그를 저장합니다.",
                "{ \"receiptId\": \"11111111-1111-1111-1111-111111111111\", \"ocrText\": \"스타벅스 아메리카노 4500원\" }",
                "{ \"logId\": \"22222222-2222-2222-2222-222222222222\", \"category\": \"카페\", \"confidence\": 0.93 }"),
            new(
                3,
                "사용자 카테고리 확정",
                "POST",
                "/api/ai/confirm-category",
                "AI 추천 결과를 사용자 확정값으로 저장해 피드백 데이터를 누적합니다.",
                "{ \"logId\": \"(2번 응답의 logId)\", \"finalCategory\": \"식비\" }",
                "{ \"logId\": \"22222222-2222-2222-2222-222222222222\", \"suggestedCategory\": \"카페\", \"finalCategory\": \"식비\", \"isCorrect\": false }"),
            new(
                4,
                "대시보드 요약 확인",
                "GET",
                "/api/ai/dashboard/summary?recentLimit=10",
                "정확도/대기건수/최근로그를 한번에 확인해 중간발표 결과를 요약합니다.",
                null,
                "{ \"generatedAt\": \"2026-01-10T01:40:00+00:00\", \"statusMessage\": \"AI 추론 데이터가 존재합니다. 최근 로그와 정확도 지표를 확인하세요.\", \"hasInferenceData\": true, \"accuracy\": { \"totalCount\": 120, \"confirmedCount\": 80, \"correctCount\": 61, \"accuracy\": 0.7625 }, \"pendingFeedbackCount\": 40, \"recentLogs\": { \"count\": 2, \"items\": [] } }"),
            new(
                5,
                "검증 실패 응답 확인",
                "POST",
                "/api/ai/confirm-category",
                "잘못된 카테고리를 전송해 ValidationProblem(400) 응답을 확인합니다.",
                "{ \"logId\": \"(2번 응답의 logId)\", \"finalCategory\": \"잘못된카테고리\" }",
                "{ \"errors\": { \"finalCategory\": [\"finalCategory는 [식비, 카페, 교통, 쇼핑, 생활, 기타] 중 하나여야 합니다.\"] } }")
        };

        return TypedResults.Ok(new AiDemoChecklistResult(steps));
    }
}
