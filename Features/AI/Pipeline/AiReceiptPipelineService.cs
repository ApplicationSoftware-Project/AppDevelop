using System.Diagnostics;
using System.Text.Json;
using App.Features.AI.Data;
using App.Features.AI.Models;
using Microsoft.SemanticKernel;

namespace App.Features.AI.Pipeline;

public sealed class AiReceiptPipelineService
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public async Task<ReceiptAnalysisResult> AnalyzeAsync(
        AnalyzeReceiptRequest request,
        Kernel kernel,
        AppDbContext db,
        CancellationToken ct = default)
    {
        var plugin = kernel.Plugins["ReceiptAnalysis"];

        using var root = AiPipelineActivitySource.Instance.StartActivity(
            "ai.receipt.analyze",
            ActivityKind.Internal);
        root?.SetTag("receipt.id", request.ReceiptId.ToString());
        root?.SetTag("ocr.length", request.OcrText.Length);

        // Step 1: OCR 텍스트 정규화
        string normalizedText;
        using (var span = AiPipelineActivitySource.Instance.StartActivity("ai.step.normalize"))
        {
            var step1 = await kernel.InvokeAsync(
                plugin["normalize_ocr_text"],
                new KernelArguments { ["rawText"] = request.OcrText },
                ct);
            normalizedText = step1.ToString();
            span?.SetTag("normalized.length", normalizedText.Length);
        }

        // Step 2: 영수증 구조 파싱
        string parsedJson;
        ParsedReceiptInfo parsedInfo;
        using (var span = AiPipelineActivitySource.Instance.StartActivity("ai.step.parse"))
        {
            var step2 = await kernel.InvokeAsync(
                plugin["parse_receipt"],
                new KernelArguments { ["normalizedText"] = normalizedText },
                ct);
            parsedJson = step2.ToString();
            parsedInfo = TryDeserialize<ParsedReceiptInfo>(parsedJson)
                ?? new ParsedReceiptInfo(null, null, null, null);
            span?.SetTag("store.name", parsedInfo.StoreName ?? "unknown");
            span?.SetTag("item.count", parsedInfo.Items?.Length ?? 0);
        }

        // Step 3: 카테고리 분류 + 근거
        string category;
        double confidence;
        string reasoning;
        using (var span = AiPipelineActivitySource.Instance.StartActivity("ai.step.classify"))
        {
            var step3 = await kernel.InvokeAsync(
                plugin["classify_category"],
                new KernelArguments
                {
                    ["parsedReceiptJson"] = parsedJson,
                    ["categoryOptions"]   = AiCategoryCatalog.OptionsText
                },
                ct);
            var classifyJson = step3.ToString();
            var classification = TryDeserialize<ReceiptClassificationAiResponse>(classifyJson)
                ?? new ReceiptClassificationAiResponse("기타", 0d, "분류 실패");

            category = AiCategoryCatalog.Options
                .Contains(classification.Category, StringComparer.OrdinalIgnoreCase)
                ? classification.Category
                : "기타";
            confidence = Math.Clamp(classification.Confidence, 0d, 1d);
            reasoning  = classification.Reasoning ?? string.Empty;

            span?.SetTag("category", category);
            span?.SetTag("confidence", confidence);
        }

        root?.SetTag("result.category", category);
        root?.SetTag("result.confidence", confidence);

        var log = new AiInferenceLog
        {
            ReceiptId         = request.ReceiptId,
            SuggestedCategory = category,
            Confidence        = confidence
        };
        db.AiInferenceLogs.Add(log);
        await db.SaveChangesAsync(ct);

        return new ReceiptAnalysisResult(log.Id, parsedInfo, category, confidence, reasoning);
    }

    private static T? TryDeserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch (JsonException) { return default; }
    }
}
