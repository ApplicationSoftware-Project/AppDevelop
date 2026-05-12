using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace App.Features.AI.Pipeline;

public sealed class ReceiptAnalysisPlugin
{
    [KernelFunction("normalize_ocr_text")]
    [Description("영수증 OCR 텍스트의 불필요한 공백·특수문자를 제거하고 읽기 쉽게 정규화합니다.")]
    public async Task<string> NormalizeOcrTextAsync(
        Kernel kernel,
        [Description("원본 OCR 텍스트")] string rawText)
    {
        var result = await kernel.InvokePromptAsync(AiPromptTemplates.NormalizeOcr + rawText);
        return result.ToString().Trim();
    }

    [KernelFunction("parse_receipt")]
    [Description("정규화된 영수증 텍스트에서 매장명·품목·금액·날짜를 추출하여 JSON으로 반환합니다.")]
    public async Task<string> ParseReceiptAsync(
        Kernel kernel,
        [Description("정규화된 영수증 텍스트")] string normalizedText)
    {
        var result = await kernel.InvokePromptAsync(AiPromptTemplates.ParseReceipt + normalizedText);
        return StripCodeFences(result.ToString());
    }

    [KernelFunction("classify_category")]
    [Description("파싱된 영수증 정보를 바탕으로 지출 카테고리를 분류하고 신뢰도와 근거를 반환합니다.")]
    public async Task<string> ClassifyCategoryAsync(
        Kernel kernel,
        [Description("파싱된 영수증 JSON")] string parsedReceiptJson,
        [Description("카테고리 선택지")] string categoryOptions)
    {
        var prompt = AiPromptTemplates.ClassifyCategory
            .Replace("{CATEGORY_OPTIONS}", categoryOptions)
            + parsedReceiptJson;
        var result = await kernel.InvokePromptAsync(prompt);
        return StripCodeFences(result.ToString());
    }

    private static string StripCodeFences(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;
        var firstNewline = trimmed.IndexOf('\n');
        var body = firstNewline >= 0 ? trimmed[(firstNewline + 1)..] : trimmed[3..];
        if (body.EndsWith("```"))
            body = body[..^3];
        return body.Trim();
    }
}
