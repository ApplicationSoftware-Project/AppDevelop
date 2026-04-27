namespace App.Features.AI;

public static class AiEndpointDescriptions
{
    public const string SuggestCategory = """
        OCR 텍스트를 기반으로 AI가 카테고리와 신뢰도를 추천하고, 추천 결과를 AiInferenceLogs에 저장합니다.

        요청 예시:
        {
          "receiptId": "11111111-1111-1111-1111-111111111111",
          "ocrText": "스타벅스 아메리카노 4500원"
        }

        성공 응답 예시(200):
        {
          "logId": "22222222-2222-2222-2222-222222222222",
          "category": "카페",
          "confidence": 0.93
        }
        """;

    public const string ConfirmCategory = """
        사용자가 AI 추천 결과를 확정하면 FinalCategory/IsCorrect를 저장해 추론 정확도 개선을 위한 피드백 데이터를 누적합니다.

        요청 예시:
        {
          "logId": "22222222-2222-2222-2222-222222222222",
          "finalCategory": "식비"
        }

        성공 응답 예시(200):
        {
          "logId": "22222222-2222-2222-2222-222222222222",
          "suggestedCategory": "카페",
          "finalCategory": "식비",
          "isCorrect": false
        }
        """;

    public const string Accuracy = """
        누적된 AI 추천 로그 중 사용자가 확정한 데이터(IsCorrect 기준)로 정확도를 계산합니다.

        성공 응답 예시(200):
        {
          "totalCount": 120,
          "confirmedCount": 80,
          "correctCount": 61,
          "accuracy": 0.7625
        }
        """;

    public const string AccuracyDaily = """
        최근 N일(days) 기준으로 사용자 확정 데이터(IsCorrect)의 일별 정확도 추이를 반환합니다.

        요청 예시: /api/ai/accuracy/daily?days=7

        성공 응답 예시(200):
        {
          "days": 7,
          "items": [
            { "date": "2026-01-01", "confirmedCount": 10, "correctCount": 8, "accuracy": 0.8 }
          ]
        }
        """;

    public const string AccuracyWeekly = """
        최근 N주(weeks) 기준으로 사용자 확정 데이터(IsCorrect)의 주별 정확도 추이를 반환합니다.

        요청 예시: /api/ai/accuracy/weekly?weeks=8

        성공 응답 예시(200):
        {
          "weeks": 8,
          "items": [
            { "weekStart": "2026-01-05", "weekEnd": "2026-01-11", "confirmedCount": 20, "correctCount": 15, "accuracy": 0.75 }
          ]
        }
        """;

    public const string AccuracyMonthly = """
        최근 N개월(months) 기준으로 사용자 확정 데이터(IsCorrect)의 월별 정확도 추이를 반환합니다.

        요청 예시: /api/ai/accuracy/monthly?months=6

        성공 응답 예시(200):
        {
          "months": 6,
          "items": [
            { "month": "2026-01", "confirmedCount": 42, "correctCount": 31, "accuracy": 0.7381 }
          ]
        }
        """;

    public const string RecentLogs = """
        최근 AI 추론 로그를 최신순으로 조회합니다.

        limit 미입력 시 기본값 20이 적용됩니다.

        요청 예시: /api/ai/logs/recent?limit=20

        성공 응답 예시(200):
        {
          "count": 2,
          "items": [
            {
              "logId": "22222222-2222-2222-2222-222222222222",
              "receiptId": "11111111-1111-1111-1111-111111111111",
              "suggestedCategory": "카페",
              "confidence": 0.93,
              "finalCategory": "식비",
              "isCorrect": false,
              "createdAt": "2026-01-10T01:23:45+00:00",
              "updatedAt": "2026-01-10T01:30:00+00:00"
            }
          ]
        }
        """;

    public const string DashboardSummary = """
        중간발표 시연을 위한 AI 대시보드 요약 정보를 제공합니다.

        포함 정보:
        - 전체 정확도(누적)
        - 사용자 확정 대기 건수(pending)
        - 최근 AI 추론 로그(recentLimit 기준)

        recentLimit 미입력 시 기본값 10이 적용됩니다.

        요청 예시: /api/ai/dashboard/summary?recentLimit=10

        성공 응답 예시(200):
        {
          "generatedAt": "2026-01-10T01:40:00+00:00",
          "statusMessage": "AI 추론 데이터가 존재합니다. 최근 로그와 정확도 지표를 확인하세요.",
          "hasInferenceData": true,
          "accuracy": {
            "totalCount": 120,
            "confirmedCount": 80,
            "correctCount": 61,
            "accuracy": 0.7625
          },
          "pendingFeedbackCount": 40,
          "recentLogs": {
            "count": 2,
            "items": []
          }
        }
        """;

    public const string DemoChecklist = """
        중간발표 시연에서 사용할 AI API 호출 순서를 제공합니다.

        권장 순서:
        1) Health 점검
        2) AI 카테고리 추천(suggest-category)
        3) 사용자 확정(confirm-category)
        4) 정확도/요약 조회(accuracy, dashboard)
        5) 예외/검증 실패 응답 확인(validation)

        각 단계에는 목적, 요청 예시(JSON), 응답 예시(JSON)가 포함됩니다.
        """;

    public const string DemoSeed = """
        중간발표 시연을 위해 AI 추론 로그 샘플 데이터를 일괄 생성합니다.

        요청 예시: /api/ai/demo/seed?total=30&confirmed=20

        파라미터:
        - total: 생성할 전체 로그 수(기본값 30, 1~500)
        - confirmed: 사용자 확정 로그 수(기본값 total의 약 2/3, 0~total)

        성공 응답 예시(200):
        {
          "insertedCount": 30,
          "confirmedCount": 20,
          "correctCount": 15,
          "pendingCount": 10
        }
        """;
}
