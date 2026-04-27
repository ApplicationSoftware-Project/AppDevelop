namespace App.Features.AI;

public static class AiPromptTemplates
{
    public const string SuggestCategory = """
        당신은 가계부 정리 전문가입니다. 
        아래의 영수증 텍스트를 분석하여 [{CATEGORY_OPTIONS}] 중 가장 적절한 카테고리 하나를 추천하세요.
        응답은 반드시 아래 JSON 형식으로만 하세요.
        { "category": "카테고리명", "confidence": 0.0~1.0 사이의 숫자 }

        영수증 내용:
        """;
}
