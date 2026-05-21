using App.Features.AI.Data;
using Microsoft.EntityFrameworkCore;
using ReceiptModel = App.Features.Receipt.Models.Receipt;

namespace App.Features.Analysis
{
    /// <summary>
    /// 영수증(Receipts) 데이터를 기반으로 카테고리/월별/요약 통계를 계산하는 서비스.
    /// 이전 구현은 AiInferenceLogs에 의존하면서 금액을 (count × 5000)으로 하드코딩했으나,
    /// 실제 지출 분석은 Receipts.Amount / Receipts.PurchasedAt을 사용해야 한다.
    /// </summary>
    public class AnalysisService
    {
        private readonly AppDbContext _context;

        public AnalysisService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 카테고리별 지출 합계. Category와 Amount가 모두 있는 영수증만 집계한다.
        /// </summary>
        public async Task<List<CategorySpending>> GetCategorySpendingAsync()
        {
            return await BuildCategorySpendingQuery()
                .OrderByDescending(x => x.TotalAmount)
                .ToListAsync();
        }

        /// <summary>
        /// 카테고리별 지출 합계 (상위 N개 / 최소 건수 필터 지원).
        /// </summary>
        public async Task<List<CategorySpending>> GetCategorySpendingAsync(int? top, int? minCount)
        {
            var query = BuildCategorySpendingQuery()
                .OrderByDescending(x => x.TotalAmount);

            IQueryable<CategorySpending> filtered = query;

            if (minCount.HasValue)
            {
                filtered = filtered.Where(x => x.TotalCount >= minCount.Value);
            }

            if (top.HasValue)
            {
                filtered = filtered.Take(top.Value);
            }

            return await filtered.ToListAsync();
        }

        /// <summary>
        /// 월별 지출 추이. 구매일(PurchasedAt) 기준으로 연/월 단위 집계한다.
        /// 구매일이 없는 영수증은 분석 대상에서 제외한다.
        /// </summary>
        public async Task<List<MonthlyTrend>> GetMonthlyTrendAsync()
        {
            return await _context.Receipts
                .AsNoTracking()
                .Where(r => r.PurchasedAt != null && r.Amount != null)
                .GroupBy(r => new
                {
                    Year = r.PurchasedAt!.Value.Year,
                    Month = r.PurchasedAt!.Value.Month
                })
                .Select(g => new MonthlyTrend
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalCount = g.Count(),
                    TotalAmount = g.Sum(r => r.Amount!.Value)
                })
                .OrderBy(t => t.Year).ThenBy(t => t.Month)
                .ToListAsync();
        }

        /// <summary>
        /// 전체 요약: 총 영수증 수 / 총 지출 / 최다 카테고리.
        /// </summary>
        public async Task<AnalysisSummary> GetSummaryAsync()
        {
            var receipts = _context.Receipts.AsNoTracking();

            var totalCount = await receipts.CountAsync();
            var totalAmount = await receipts
                .Where(r => r.Amount != null)
                .SumAsync(r => r.Amount!.Value);

            var topCategory = await receipts
                .Where(r => r.Category != null)
                .GroupBy(r => r.Category!)
                .Select(g => new
                {
                    Category = g.Key,
                    Total = g.Where(r => r.Amount != null)
                             .Sum(r => r.Amount!.Value)
                })
                .OrderByDescending(x => x.Total)
                .Select(x => x.Category)
                .FirstOrDefaultAsync();

            return new AnalysisSummary
            {
                TotalCount = totalCount,
                TotalAmount = totalAmount,
                TopCategory = topCategory,
                GeneratedAt = DateTimeOffset.UtcNow
            };
        }

        /// <summary>
        /// 카테고리별 집계의 공통 쿼리 빌더.
        /// Category가 null이 아니고 Amount가 null이 아닌 행만 대상으로 한다.
        /// </summary>
        private IQueryable<CategorySpending> BuildCategorySpendingQuery()
        {
            return _context.Receipts
                .AsNoTracking()
                .Where(r => r.Category != null && r.Amount != null)
                .GroupBy(r => r.Category!)
                .Select(g => new CategorySpending
                {
                    Category = g.Key,
                    TotalCount = g.Count(),
                    TotalAmount = g.Sum(r => r.Amount!.Value)
                });
        }
    }
}
