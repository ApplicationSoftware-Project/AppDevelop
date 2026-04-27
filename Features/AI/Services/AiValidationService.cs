namespace App.Features.AI.Services;

public static class AiValidationService
{
    public static Dictionary<string, string[]>? ValidateSuggestCategoryRequest(SuggestCategoryRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.ReceiptId == Guid.Empty)
        {
            errors[nameof(request.ReceiptId)] = ["receiptId는 비어 있을 수 없습니다."];
        }

        if (string.IsNullOrWhiteSpace(request.OcrText))
        {
            errors[nameof(request.OcrText)] = ["ocrText는 비어 있을 수 없습니다."];
        }

        return errors.Count > 0 ? errors : null;
    }

    public static (Dictionary<string, string[]>? Errors, string? NormalizedFinalCategory) ValidateAndNormalizeConfirmCategoryRequest(ConfirmCategoryRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.LogId == Guid.Empty)
        {
            errors[nameof(request.LogId)] = ["logId는 비어 있을 수 없습니다."];
        }

        if (string.IsNullOrWhiteSpace(request.FinalCategory))
        {
            errors[nameof(request.FinalCategory)] = ["finalCategory는 비어 있을 수 없습니다."];
            return (errors, null);
        }

        var finalCategory = request.FinalCategory.Trim();
        if (finalCategory.Length > 200)
        {
            finalCategory = finalCategory[..200];
        }

        if (!AiCategoryCatalog.Options.Contains(finalCategory, StringComparer.OrdinalIgnoreCase))
        {
            errors[nameof(request.FinalCategory)] = [$"finalCategory는 [{AiCategoryCatalog.OptionsText}] 중 하나여야 합니다."];
        }

        return errors.Count > 0 ? (errors, null) : (null, finalCategory);
    }
}
