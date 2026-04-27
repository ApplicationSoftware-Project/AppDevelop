using System.Text.Json;
using App.Data;
using App.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace App.Features.AI;

public static class AiEndpoints
{
    private static readonly string[] CategoryOptions = ["식비", "카페", "교통", "쇼핑", "생활", "기타"];
    private static readonly string CategoryOptionsText = string.Join(", ", CategoryOptions);

    public static void MapAiEndpoints(this WebApplication app)
    {
        app.MapPost("/api/ai/suggest-category", SuggestCategory)
            .WithName("SuggestCategory")
            .WithSummary("영수증 OCR 텍스트 기반 AI 카테고리 추천")
            .WithDescription(
                "OCR 텍스트를 기반으로 AI가 카테고리와 신뢰도를 추천하고, 추천 결과를 AiInferenceLogs에 저장합니다.\n\n"
                + "요청 예시:\n"
                + "{\n"
                + "  \"receiptId\": \"11111111-1111-1111-1111-111111111111\",\n"
                + "  \"ocrText\": \"스타벅스 아메리카노 4500원\"\n"
                + "}\n\n"
                + "성공 응답 예시(200):\n"
                + "{\n"
                + "  \"logId\": \"22222222-2222-2222-2222-222222222222\",\n"
                + "  \"category\": \"카페\",\n"
                + "  \"confidence\": 0.93\n"
                + "}")
            .Accepts<SuggestCategoryRequest>("application/json")
            .Produces<SuggestCategoryResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        app.MapPost("/api/ai/confirm-category", ConfirmCategory)
            .WithName("ConfirmCategory")
            .WithSummary("AI 추천 카테고리 사용자 확정")
            .WithDescription(
                "사용자가 AI 추천 결과를 확정하면 FinalCategory/IsCorrect를 저장해 추론 정확도 개선을 위한 피드백 데이터를 누적합니다.\n\n"
                + "요청 예시:\n"
                + "{\n"
                + "  \"logId\": \"22222222-2222-2222-2222-222222222222\",\n"
                + "  \"finalCategory\": \"식비\"\n"
                + "}\n\n"
                + "성공 응답 예시(200):\n"
                + "{\n"
                + "  \"logId\": \"22222222-2222-2222-2222-222222222222\",\n"
                + "  \"suggestedCategory\": \"카페\",\n"
                + "  \"finalCategory\": \"식비\",\n"
                + "  \"isCorrect\": false\n"
                + "}")
            .Accepts<ConfirmCategoryRequest>("application/json")
            .Produces<ConfirmCategoryResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/ai/accuracy", GetAiAccuracy)
            .WithName("GetAiAccuracy")
            .WithSummary("AI 카테고리 추천 정확도 조회")
            .WithDescription(
                "누적된 AI 추천 로그 중 사용자가 확정한 데이터(IsCorrect 기준)로 정확도를 계산합니다.\n\n"
                + "성공 응답 예시(200):\n"
                + "{\n"
                + "  \"totalCount\": 120,\n"
                + "  \"confirmedCount\": 80,\n"
                + "  \"correctCount\": 61,\n"
                + "  \"accuracy\": 0.7625\n"
                + "}")
            .Produces<AiAccuracyResult>(StatusCodes.Status200OK);

        app.MapGet("/api/ai/accuracy/daily", GetAiAccuracyDaily)
            .WithName("GetAiAccuracyDaily")
            .WithSummary("AI 일별 카테고리 추천 정확도 조회")
            .WithDescription(
                "최근 N일(days) 기준으로 사용자 확정 데이터(IsCorrect)의 일별 정확도 추이를 반환합니다.\n\n"
                + "요청 예시: /api/ai/accuracy/daily?days=7\n\n"
                + "성공 응답 예시(200):\n"
                + "{\n"
                + "  \"days\": 7,\n"
                + "  \"items\": [\n"
                + "    { \"date\": \"2026-01-01\", \"confirmedCount\": 10, \"correctCount\": 8, \"accuracy\": 0.8 }\n"
                + "  ]\n"
                + "}")
            .Produces<AiAccuracyDailyResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/ai/accuracy/weekly", GetAiAccuracyWeekly)
            .WithName("GetAiAccuracyWeekly")
            .WithSummary("AI 주별 카테고리 추천 정확도 조회")
            .WithDescription(
                "최근 N주(weeks) 기준으로 사용자 확정 데이터(IsCorrect)의 주별 정확도 추이를 반환합니다.\n\n"
                + "요청 예시: /api/ai/accuracy/weekly?weeks=8\n\n"
                + "성공 응답 예시(200):\n"
                + "{\n"
                + "  \"weeks\": 8,\n"
                + "  \"items\": [\n"
                + "    { \"weekStart\": \"2026-01-05\", \"weekEnd\": \"2026-01-11\", \"confirmedCount\": 20, \"correctCount\": 15, \"accuracy\": 0.75 }\n"
                + "  ]\n"
                + "}")
            .Produces<AiAccuracyWeeklyResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/ai/accuracy/monthly", GetAiAccuracyMonthly)
            .WithName("GetAiAccuracyMonthly")
            .WithSummary("AI 월별 카테고리 추천 정확도 조회")
            .WithDescription(
                "최근 N개월(months) 기준으로 사용자 확정 데이터(IsCorrect)의 월별 정확도 추이를 반환합니다.\n\n"
                + "요청 예시: /api/ai/accuracy/monthly?months=6\n\n"
                + "성공 응답 예시(200):\n"
                + "{\n"
                + "  \"months\": 6,\n"
                + "  \"items\": [\n"
                + "    { \"month\": \"2026-01\", \"confirmedCount\": 42, \"correctCount\": 31, \"accuracy\": 0.7381 }\n"
                + "  ]\n"
                + "}")
            .Produces<AiAccuracyMonthlyResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);
    }

    private static async Task<Results<Ok<SuggestCategoryResult>, ValidationProblem, ProblemHttpResult>> SuggestCategory(
        SuggestCategoryRequest request,
        Kernel k,
        AppDbContext db,
        ILogger<Program> logger)
    {
        if (request.ReceiptId == Guid.Empty)
        {
            logger.LogWarning("suggest-category 요청 거부: receiptId가 비어 있음");
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ReceiptId)] = ["receiptId는 비어 있을 수 없습니다."]
            });
        }

        if (string.IsNullOrWhiteSpace(request.OcrText))
        {
            logger.LogWarning("suggest-category 요청 거부: ocrText가 비어 있음");
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.OcrText)] = ["ocrText는 비어 있을 수 없습니다."]
            });
        }

        var promptTemplate = """
            당신은 가계부 정리 전문가입니다. 
            아래의 영수증 텍스트를 분석하여 [{CATEGORY_OPTIONS}] 중 가장 적절한 카테고리 하나를 추천하세요.
            응답은 반드시 아래 JSON 형식으로만 하세요.
            { "category": "카테고리명", "confidence": 0.0~1.0 사이의 숫자 }

            영수증 내용:
            """;

        promptTemplate = promptTemplate.Replace("{CATEGORY_OPTIONS}", CategoryOptionsText);

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

            if (!CategoryOptions.Contains(category, StringComparer.OrdinalIgnoreCase))
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

        var finalCategory = request.FinalCategory.Trim();
        if (finalCategory.Length > 200)
        {
            finalCategory = finalCategory[..200];
        }

        if (!CategoryOptions.Contains(finalCategory, StringComparer.OrdinalIgnoreCase))
        {
            logger.LogWarning("confirm-category 요청 거부: 허용되지 않은 finalCategory. value={FinalCategory}", finalCategory);
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.FinalCategory)] = [$"finalCategory는 [{CategoryOptionsText}] 중 하나여야 합니다."]
            });
        }

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

    private static async Task<Ok<AiAccuracyResult>> GetAiAccuracy(AppDbContext db)
    {
        var totalCount = await db.AiInferenceLogs.CountAsync();
        var confirmedCount = await db.AiInferenceLogs.CountAsync(x => x.IsCorrect.HasValue);
        var correctCount = await db.AiInferenceLogs.CountAsync(x => x.IsCorrect == true);

        var accuracy = confirmedCount == 0
            ? 0d
            : Math.Round((double)correctCount / confirmedCount, 4);

        return TypedResults.Ok(new AiAccuracyResult(
            totalCount,
            confirmedCount,
            correctCount,
            accuracy));
    }

    private static async Task<Results<Ok<AiAccuracyDailyResult>, ValidationProblem>> GetAiAccuracyDaily(int days, AppDbContext db)
    {
        if (days < 1 || days > 365)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(days)] = ["days는 1 이상 365 이하여야 합니다."]
            });
        }

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-(days - 1)));
        var startUtc = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtcExclusive = startUtc.AddDays(days);

        var dailyGrouped = await db.AiInferenceLogs
            .Where(x => x.IsCorrect.HasValue && x.CreatedAt >= startUtc && x.CreatedAt < endUtcExclusive)
            .GroupBy(x => EF.Functions.DateDiffDay(startUtc, x.CreatedAt))
            .Select(g => new
            {
                DayOffset = g.Key,
                ConfirmedCount = g.Count(),
                CorrectCount = g.Sum(x => x.IsCorrect == true ? 1 : 0)
            })
            .ToDictionaryAsync(x => x.DayOffset);

        var dailyStats = Enumerable.Range(0, days)
            .Select(offset =>
            {
                var date = startDate.AddDays(offset);
                dailyGrouped.TryGetValue(offset, out var grouped);

                var confirmedCount = grouped?.ConfirmedCount ?? 0;
                var correctCount = grouped?.CorrectCount ?? 0;
                var accuracy = confirmedCount == 0
                    ? 0d
                    : Math.Round((double)correctCount / confirmedCount, 4);

                return new AiAccuracyDailyItem(
                    date.ToString("yyyy-MM-dd"),
                    confirmedCount,
                    correctCount,
                    accuracy);
            })
            .ToList();

        return TypedResults.Ok(new AiAccuracyDailyResult(days, dailyStats));
    }

    private static async Task<Results<Ok<AiAccuracyWeeklyResult>, ValidationProblem>> GetAiAccuracyWeekly(int weeks, AppDbContext db)
    {
        if (weeks < 1 || weeks > 104)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(weeks)] = ["weeks는 1 이상 104 이하여야 합니다."]
            });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var currentWeekStart = GetWeekStart(today);
        var startWeek = currentWeekStart.AddDays(-(weeks - 1) * 7);
        var startUtc = startWeek.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtcExclusive = startUtc.AddDays(weeks * 7);

        var dailyGrouped = await db.AiInferenceLogs
            .Where(x => x.IsCorrect.HasValue && x.CreatedAt >= startUtc && x.CreatedAt < endUtcExclusive)
            .GroupBy(x => EF.Functions.DateDiffDay(startUtc, x.CreatedAt))
            .Select(g => new
            {
                DayOffset = g.Key,
                ConfirmedCount = g.Count(),
                CorrectCount = g.Sum(x => x.IsCorrect == true ? 1 : 0)
            })
            .ToDictionaryAsync(x => x.DayOffset);

        var weeklyStats = Enumerable.Range(0, weeks)
            .Select(offset => new
            {
                Offset = offset,
                WeekStart = startWeek.AddDays(offset * 7)
            })
            .Select(x =>
            {
                var weekStart = x.WeekStart;
                var weekEnd = weekStart.AddDays(6);
                var weekOffset = x.Offset * 7;
                var confirmedCount = Enumerable.Range(0, 7)
                    .Sum(day => dailyGrouped.TryGetValue(weekOffset + day, out var grouped) ? grouped.ConfirmedCount : 0);
                var correctCount = Enumerable.Range(0, 7)
                    .Sum(day => dailyGrouped.TryGetValue(weekOffset + day, out var grouped) ? grouped.CorrectCount : 0);
                var accuracy = confirmedCount == 0
                    ? 0d
                    : Math.Round((double)correctCount / confirmedCount, 4);

                return new AiAccuracyWeeklyItem(
                    weekStart.ToString("yyyy-MM-dd"),
                    weekEnd.ToString("yyyy-MM-dd"),
                    confirmedCount,
                    correctCount,
                    accuracy);
            })
            .ToList();

        return TypedResults.Ok(new AiAccuracyWeeklyResult(weeks, weeklyStats));
    }

    private static async Task<Results<Ok<AiAccuracyMonthlyResult>, ValidationProblem>> GetAiAccuracyMonthly(int months, AppDbContext db)
    {
        if (months < 1 || months > 36)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(months)] = ["months는 1 이상 36 이하여야 합니다."]
            });
        }

        var currentMonthStart = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var startMonth = currentMonthStart.AddMonths(-(months - 1));
        var startUtc = startMonth.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtcExclusive = startUtc.AddMonths(months);

        var monthlyGrouped = await db.AiInferenceLogs
            .Where(x => x.IsCorrect.HasValue && x.CreatedAt >= startUtc && x.CreatedAt < endUtcExclusive)
            .GroupBy(x => new { x.CreatedAt.Year, x.CreatedAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                ConfirmedCount = g.Count(),
                CorrectCount = g.Sum(x => x.IsCorrect == true ? 1 : 0)
            })
            .ToDictionaryAsync(x => (x.Year, x.Month));

        var monthlyStats = Enumerable.Range(0, months)
            .Select(offset => startMonth.AddMonths(offset))
            .Select(monthStart =>
            {
                monthlyGrouped.TryGetValue((monthStart.Year, monthStart.Month), out var grouped);
                var confirmedCount = grouped?.ConfirmedCount ?? 0;
                var correctCount = grouped?.CorrectCount ?? 0;
                var accuracy = confirmedCount == 0
                    ? 0d
                    : Math.Round((double)correctCount / confirmedCount, 4);

                return new AiAccuracyMonthlyItem(
                    monthStart.ToString("yyyy-MM"),
                    confirmedCount,
                    correctCount,
                    accuracy);
            })
            .ToList();

        return TypedResults.Ok(new AiAccuracyMonthlyResult(months, monthlyStats));
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff);
    }
}
