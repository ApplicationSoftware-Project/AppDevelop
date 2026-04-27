using App.Features.AI.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Features.AI.Services;

public sealed class AiAccuracyService
{
    public async Task<AiAccuracyResult> GetOverallAsync(AppDbContext db)
    {
        var totalCount = await db.AiInferenceLogs.CountAsync();
        var confirmedCount = await db.AiInferenceLogs.CountAsync(x => x.IsCorrect.HasValue);
        var correctCount = await db.AiInferenceLogs.CountAsync(x => x.IsCorrect == true);

        var accuracy = confirmedCount == 0
            ? 0d
            : Math.Round((double)correctCount / confirmedCount, 4);

        return new AiAccuracyResult(totalCount, confirmedCount, correctCount, accuracy);
    }

    public async Task<AiAccuracyDailyResult> GetDailyAsync(int days, AppDbContext db)
    {
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

        return new AiAccuracyDailyResult(days, dailyStats);
    }

    public async Task<AiAccuracyWeeklyResult> GetWeeklyAsync(int weeks, AppDbContext db)
    {
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

        return new AiAccuracyWeeklyResult(weeks, weeklyStats);
    }

    public async Task<AiAccuracyMonthlyResult> GetMonthlyAsync(int months, AppDbContext db)
    {
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

        return new AiAccuracyMonthlyResult(months, monthlyStats);
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff);
    }
}
