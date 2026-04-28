namespace App.Features.Analysis
{
    /// <summary>
    /// API response for the analysis feature
    /// define the DTO
    /// </summary>
    public class AnalysisContracts
    {
    }

    public sealed class AnalysisSummary
    {
        public int TotalCount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? TopCategory { get; set; }
        public DateTimeOffset GeneratedAt { get; set; }
    }

    public sealed class CategorySpending
    {
        public string Category { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public sealed class MonthlyTrend
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int TotalCount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public sealed record AnalysisDemoChecklistResult(IReadOnlyList<AnalysisDemoChecklistStep> Steps);

    public sealed record AnalysisDemoChecklistStep(
        int Order,
        string Name,
        string Method,
        string Path,
        string Purpose,
        string? ExampleRequest,
        string? ExampleResponse);
}
