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
}
