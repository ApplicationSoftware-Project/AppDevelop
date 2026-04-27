using System.Text.Json;
using App.Features.AI.Data;
using App.Features.AI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace App.Features.AI.Services;

public sealed class AiSuggestionService
{
    public async Task<SuggestCategoryResult> SuggestCategoryAsync(SuggestCategoryRequest request, Kernel kernel, AppDbContext db)
    {
        var promptTemplate = AiPromptTemplates.SuggestCategory
            .Replace("{CATEGORY_OPTIONS}", AiCategoryCatalog.OptionsText);

        var prompt = promptTemplate + request.OcrText;

        var result = await kernel.InvokePromptAsync(prompt);
        var responseText = result.ToString();

        try
        {
            var parsed = JsonSerializer.Deserialize<SuggestCategoryAiResponse>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Category))
            {
                throw new AiFeatureException("AI 응답을 해석할 수 없습니다.", StatusCodes.Status502BadGateway);
            }

            var category = parsed.Category.Trim();
            if (category.Length > 200)
            {
                category = category[..200];
            }

            if (!AiCategoryCatalog.Options.Contains(category, StringComparer.OrdinalIgnoreCase))
            {
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

            return new SuggestCategoryResult(log.Id, category, confidence);
        }
        catch (JsonException ex)
        {
            throw new AiFeatureException("AI 응답 형식이 올바르지 않습니다.", StatusCodes.Status502BadGateway, ex);
        }
        catch (DbUpdateException ex)
        {
            throw new AiFeatureException("AI 추천 로그 저장 중 오류가 발생했습니다.", StatusCodes.Status500InternalServerError, ex);
        }
    }
}
