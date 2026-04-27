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

    private static async Task<Results<Ok<AiRecentLogsResult>, ValidationProblem>> GetAiRecentLogs(int limit, AppDbContext db, AiLogQueryService logQueryService)
    {
        if (limit < 1 || limit > 200)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(limit)] = ["limit은 1 이상 200 이하여야 합니다."]
            });
        }

        var result = await logQueryService.GetRecentLogsAsync(limit, db);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<AiDashboardSummaryResult>, ValidationProblem>> GetAiDashboardSummary(
        int recentLimit,
        AppDbContext db,
        AiAccuracyService accuracyService,
        AiLogQueryService logQueryService,
        AiDashboardService dashboardService)
    {
        if (recentLimit < 1 || recentLimit > 50)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(recentLimit)] = ["recentLimit은 1 이상 50 이하여야 합니다."]
            });
        }

        var result = await dashboardService.GetSummaryAsync(recentLimit, db, accuracyService, logQueryService);
        return TypedResults.Ok(result);
    }
}
