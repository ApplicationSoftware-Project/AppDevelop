using System.Text.Json;
using App.Features.AI.Data;
using App.Features.AI.Models;
using App.Features.AI.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
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
    }

    private static async Task<Results<Ok<SuggestCategoryResult>, ValidationProblem, ProblemHttpResult>> SuggestCategory(
        SuggestCategoryRequest request,
        Kernel k,
        AppDbContext db,
        ILogger<Program> logger)
    {
        var suggestValidation = AiValidationService.ValidateSuggestCategoryRequest(request);
        if (suggestValidation is not null)
        {
            logger.LogWarning("suggest-category 요청 거부: Validation 실패");
            return TypedResults.ValidationProblem(suggestValidation);
        }

        var promptTemplate = AiPromptTemplates.SuggestCategory
            .Replace("{CATEGORY_OPTIONS}", AiCategoryCatalog.OptionsText);

        var prompt = promptTemplate + request.OcrText;

        var result = await k.InvokePromptAsync(prompt);
        var responseText = result.ToString();

        try
        {
            var parsed = JsonSerializer.Deserialize<SuggestCategoryAiResponse>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Category))
            {
                logger.LogWarning("AI 응답 파싱 실패: category 누락 또는 null. raw={ResponseText}", responseText);
                return TypedResults.Problem(
                    detail: "AI 응답을 해석할 수 없습니다.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            var category = parsed.Category.Trim();
            if (category.Length > 200)
            {
                category = category[..200];
            }

            if (!AiCategoryCatalog.Options.Contains(category, StringComparer.OrdinalIgnoreCase))
            {
                logger.LogWarning("AI 추천 카테고리가 허용 목록에 없어 '기타'로 대체합니다. rawCategory={RawCategory}", category);
                category = "기타";
            }

            var confidence = Math.Clamp(parsed.Confidence, 0d, 1d);

            var log = new AiInferenceLog
            {
                ReceiptId = request.ReceiptId,
                SuggestedCategory = category,
                Confidence = confidence
            };

            db.AiInferenceLogs.Add(log);
            await db.SaveChangesAsync();

            logger.LogInformation("AI 추천 로그 저장 완료. LogId={LogId}, ReceiptId={ReceiptId}, Category={Category}, Confidence={Confidence}",
                log.Id, request.ReceiptId, category, confidence);

            return TypedResults.Ok(new SuggestCategoryResult(
                log.Id,
                category,
                confidence));
        }
        catch (DbUpdateException)
        {
            logger.LogError("AI 추천 로그 DB 저장 실패. ReceiptId={ReceiptId}", request.ReceiptId);
            return TypedResults.Problem(
                detail: "AI 추천 로그 저장 중 오류가 발생했습니다.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (JsonException)
        {
            logger.LogWarning("AI 응답 JSON 형식 오류. raw={ResponseText}", responseText);
            return TypedResults.Problem(
                detail: "AI 응답 형식이 올바르지 않습니다.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<Results<Ok<ConfirmCategoryResult>, ValidationProblem, NotFound, ProblemHttpResult>> ConfirmCategory(
        ConfirmCategoryRequest request,
        AppDbContext db,
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

        var log = await db.AiInferenceLogs.FindAsync(request.LogId);
        if (log is null)
        {
            logger.LogWarning("confirm-category 대상 로그 없음. LogId={LogId}", request.LogId);
            return TypedResults.NotFound();
        }

        log.FinalCategory = finalCategory;
        log.IsCorrect = string.Equals(log.SuggestedCategory, finalCategory, StringComparison.OrdinalIgnoreCase);
        log.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync();

            logger.LogInformation("AI 피드백 저장 완료. LogId={LogId}, Suggested={SuggestedCategory}, Final={FinalCategory}, IsCorrect={IsCorrect}",
                log.Id, log.SuggestedCategory, log.FinalCategory, log.IsCorrect);

            return TypedResults.Ok(new ConfirmCategoryResult(
                log.Id,
                log.SuggestedCategory,
                log.FinalCategory,
                log.IsCorrect.GetValueOrDefault()));
        }
        catch (DbUpdateException)
        {
            logger.LogError("AI 피드백 DB 저장 실패. LogId={LogId}", request.LogId);
            return TypedResults.Problem(
                detail: "AI 피드백 저장 중 오류가 발생했습니다.",
                statusCode: StatusCodes.Status500InternalServerError);
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
}
