namespace App.Features.AI;

public sealed record SuggestCategoryRequest(Guid ReceiptId, string OcrText);
public sealed record SuggestCategoryAiResponse(string Category, double Confidence);
public sealed record SuggestCategoryResult(Guid LogId, string Category, double Confidence);
public sealed record ConfirmCategoryRequest(Guid LogId, string FinalCategory);
public sealed record ConfirmCategoryResult(Guid LogId, string SuggestedCategory, string? FinalCategory, bool IsCorrect);
public sealed record AiAccuracyResult(int TotalCount, int ConfirmedCount, int CorrectCount, double Accuracy);
public sealed record AiAccuracyDailyResult(int Days, IReadOnlyList<AiAccuracyDailyItem> Items);
public sealed record AiAccuracyDailyItem(string Date, int ConfirmedCount, int CorrectCount, double Accuracy);
public sealed record AiAccuracyWeeklyResult(int Weeks, IReadOnlyList<AiAccuracyWeeklyItem> Items);
public sealed record AiAccuracyWeeklyItem(string WeekStart, string WeekEnd, int ConfirmedCount, int CorrectCount, double Accuracy);
public sealed record AiAccuracyMonthlyResult(int Months, IReadOnlyList<AiAccuracyMonthlyItem> Items);
public sealed record AiAccuracyMonthlyItem(string Month, int ConfirmedCount, int CorrectCount, double Accuracy);
public sealed record AiRecentLogsResult(int Count, IReadOnlyList<AiRecentLogItem> Items);
public sealed record AiRecentLogItem(
    Guid LogId,
    Guid ReceiptId,
    string SuggestedCategory,
    double Confidence,
    string? FinalCategory,
    bool? IsCorrect,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
public sealed record AiDashboardSummaryResult(
    DateTimeOffset GeneratedAt,
    AiAccuracyResult Accuracy,
    int PendingFeedbackCount,
    AiRecentLogsResult RecentLogs);
