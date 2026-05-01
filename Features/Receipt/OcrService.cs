using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace App.Features.Receipt;

/// <summary>
/// 영수증 이미지를 Gemini 비전 모델에 직접 전달해 상호/금액/일자/원본 텍스트를 추출.
/// 별도 OCR 서비스 없이 LLM이 이미지 인식과 필드 추출을 한 번에 수행한다.
/// </summary>
public class OcrService(Kernel kernel, ILogger<OcrService> logger)
{
    private const string SystemPrompt = """
        너는 한국 영수증을 분석하는 전문가다. 사용자가 제공한 영수증 이미지를 보고 아래 4개 필드를 추출해 JSON으로만 응답해라.

        {
          "merchantName": "<상호명. 명확하지 않으면 빈 문자열>",
          "totalAmount": <최종 결제 금액. 숫자(KRW). 합계/결제금액/총액에 해당하는 값을 우선. 부가세, 할인, 단가는 제외. 알 수 없으면 0>,
          "transactionDate": "<영수증의 어떤 위치에 어떤 형식이든 거래 시점을 나타내는 날짜를 찾아 yyyy-MM-dd로 변환해라. 없으면 빈 문자열>",
          "rawText": "<영수증에서 읽은 모든 텍스트. 줄바꿈 포함>"
        }

        규칙:
        - JSON 외에 어떤 텍스트도 포함하지 마라. 마크다운 코드 펜스 ```도 안 된다.
        - 전화번호, 사업자번호, 카드번호 등 금액이 아닌 숫자를 합계로 오인하지 마라.
        - 한국 통화 표기(7,300원, ₩7,300, 7300)는 모두 정수 7300으로 변환해라.
        """;

    public async Task<OcrResult> ParseAsync(string imagePath, CancellationToken ct = default)
    {
        var imageBytes = await File.ReadAllBytesAsync(imagePath, ct);
        var contentType = GetContentType(imagePath);

        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(SystemPrompt);
        history.AddUserMessage(
        [
            new TextContent("이 영수증을 분석해 JSON으로 응답해줘."),
            new ImageContent(imageBytes, contentType)
        ]);

        string responseText;
        try
        {
            var response = await chat.GetChatMessageContentAsync(history, kernel: kernel, cancellationToken: ct);
            responseText = response.Content ?? string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gemini OCR 호출 실패. ImagePath={Path}", imagePath);
            return Empty();
        }

        var json = StripCodeFences(responseText);

        try
        {
            var dto = JsonSerializer.Deserialize<GeminiOcrDto>(json, JsonOpts);
            if (dto is null)
            {
                logger.LogWarning("Gemini이 빈 응답을 반환했습니다. Raw={Raw}", Truncate(responseText));
                return Empty();
            }

            return new OcrResult(
                StoreName: string.IsNullOrWhiteSpace(dto.MerchantName) ? "알 수 없는 상점" : dto.MerchantName.Trim(),
                Amount: dto.TotalAmount > 0 ? (decimal)dto.TotalAmount : 0m,
                PurchasedAt: ParseDate(dto.TransactionDate),
                RawText: dto.RawText ?? string.Empty);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Gemini JSON 파싱 실패. Raw={Raw}", Truncate(responseText));
            return Empty();
        }
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

    private static string GetContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };

    private static DateTimeOffset ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DateTimeOffset.UtcNow;
        if (DateTime.TryParse(raw, out var d))
            return new DateTimeOffset(d.Year, d.Month, d.Day, 0, 0, 0, TimeSpan.FromHours(9));
        return DateTimeOffset.UtcNow;
    }

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500] + "...";

    private static OcrResult Empty() => new(string.Empty, 0m, DateTimeOffset.UtcNow, string.Empty);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private record GeminiOcrDto(
        string? MerchantName,
        double TotalAmount,
        string? TransactionDate,
        string? RawText);
}
