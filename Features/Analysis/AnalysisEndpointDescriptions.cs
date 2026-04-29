namespace App.Features.Analysis;

public static class AnalysisEndpointDescriptions
{
    public const string Summary = """
        분석 서비스의 요약 지표를 반환합니다.

        포함 정보:
        - 전체 로그 수
        - 추정 총액(샘플 단가 기준)
        - 최다 카테고리

        성공 응답 예시(200):
        {
          "totalCount": 120,
          "totalAmount": 600000,
          "topCategory": "식비",
          "generatedAt": "2026-04-29T01:20:00+00:00"
        }
        """;

    public const string CategoryTotal = """
        카테고리별 지출 합계를 반환합니다.

        쿼리 파라미터:
        - top: 상위 N개만 반환
        - minCount: 최소 건수 필터

        요청 예시: /api/analysis/category-total?top=3&minCount=2

        성공 응답 예시(200):
        [
          { "category": "식비", "totalCount": 32, "totalAmount": 160000 }
        ]
        """;

    public const string MonthlyTrend = """
        월별 지출 추이를 반환합니다.

        성공 응답 예시(200):
        [
          { "year": 2026, "month": 4, "totalCount": 45, "totalAmount": 225000 }
        ]
        """;

    public const string DemoChecklist = """
        중간발표 시연을 위한 분석 API 호출 순서를 제공합니다.

        권장 순서:
        1) 요약 지표 조회
        2) 카테고리별 합계 조회
        3) 월별 추이 조회

        각 단계에는 목적, 요청 예시, 응답 예시가 포함됩니다.
        """;
}
