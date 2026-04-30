using System.Text.RegularExpressions;

namespace App.Features.Receipt;

/// <summary>
/// 영수증 이미지 OCR. 실제 OCR API(Azure AI Vision / CLOVA 등) 연동 전까지 빈 결과를 반환하는 스텁.
/// 실제 연동 시 ParseAsync 안에서 이미지 → 텍스트 추출 후 ExtractStoreName/Amount/Date 헬퍼 재사용.
/// </summary>
public partial class OcrService
{
    private static readonly string[] StoreKeywords =
        ["스타벅스", "맥도날드", "GS25", "CU", "이마트", "코스트코", "올리브영", "배달의민족", "쿠팡"];

    public Task<OcrResult> ParseAsync(string imagePath, CancellationToken ct = default)
    {
        // TODO: 실제 OCR 연동 — 이미지를 읽어 rawText를 얻고 아래 헬퍼로 필드 추출.
        var stub = new OcrResult(
            StoreName: string.Empty,
            Amount: 0m,
            PurchasedAt: DateTimeOffset.UtcNow,
            RawText: string.Empty);
        return Task.FromResult(stub);
    }

    private static string ExtractStoreName(string text)
    {
        foreach (var keyword in StoreKeywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return keyword;
        }

        var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        return firstLine ?? "알 수 없는 상점";
    }

    private static decimal ExtractAmount(string text)
    {
        var matches = AmountPattern().Matches(text);
        foreach (Match m in matches)
        {
            var digits = m.Value.Replace(",", "").Replace("원", "").Trim();
            if (decimal.TryParse(digits, out var amount) && amount > 0)
                return amount;
        }
        return 0m;
    }

    private static DateTimeOffset ExtractDate(string text)
    {
        var m = DatePattern().Match(text);
        if (m.Success &&
            int.TryParse(m.Groups["y"].Value, out var y) &&
            int.TryParse(m.Groups["mo"].Value, out var mo) &&
            int.TryParse(m.Groups["d"].Value, out var d))
        {
            try { return new DateTimeOffset(y, mo, d, 0, 0, 0, TimeSpan.FromHours(9)); }
            catch { }
        }
        return DateTimeOffset.UtcNow;
    }

    [GeneratedRegex(@"[\d,]+원?", RegexOptions.None)]
    private static partial Regex AmountPattern();

    [GeneratedRegex(@"(?<y>\d{4})[-./](?<mo>\d{1,2})[-./](?<d>\d{1,2})")]
    private static partial Regex DatePattern();
}
