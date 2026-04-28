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
}
