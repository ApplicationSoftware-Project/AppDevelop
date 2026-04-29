using System.Text.RegularExpressions;

namespace App.Features.Receipt;

/// <summary>
/// OCR 스텁 구현. 실제 OCR API(Azure AI Vision 등) 연동 전까지 텍스트 파싱으로 대체.
/// </summary>
public partial class OcrService
{
    private static readonly string[] StoreKeywords =
        ["스타벅스", "맥도날드", "GS25", "CU", "이마트", "코스트코", "올리브영", "배달의민족", "쿠팡"];

    public OcrResult Parse(string rawText)
    {
        var storeName = ExtractStoreName(rawText);
        var amount = ExtractAmount(rawText);
        var purchasedAt = ExtractDate(rawText);

        return new OcrResult(storeName, amount, purchasedAt, rawText);
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