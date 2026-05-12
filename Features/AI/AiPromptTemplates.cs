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

    // 멀티스텝 파이프라인용 프롬프트

    public const string NormalizeOcr = """
        당신은 OCR 텍스트 정규화 전문가입니다.
        아래 OCR 텍스트에서 중복 공백, 불필요한 특수문자, 인식 오류 문자를 제거하고
        자연스럽게 읽을 수 있는 텍스트로 변환하세요.
        매장명, 품목, 금액, 날짜 등 영수증 핵심 정보를 최대한 보존하세요.
        정규화된 텍스트만 출력하고 설명은 하지 마세요.

        원본 OCR 텍스트:
        """;

    public const string ParseReceipt = """
        당신은 영수증 정보 추출 전문가입니다.
        아래 영수증 텍스트에서 구조화된 정보를 추출하여 JSON으로 반환하세요.
        알 수 없는 값은 null로 설정하세요.
        응답은 반드시 아래 JSON 형식으로만 하세요:
        { "storeName": "매장명 또는 null", "items": ["품목1", "품목2"] 또는 null, "totalAmount": 금액숫자 또는 null, "date": "YYYY-MM-DD 또는 null" }

        영수증 텍스트:
        """;

    public const string ClassifyCategory = """
        당신은 가계부 정리 전문가입니다.
        아래 영수증 정보를 분석하여 [{CATEGORY_OPTIONS}] 중 가장 적절한 카테고리를 분류하세요.
        응답은 반드시 아래 JSON 형식으로만 하세요:
        { "category": "카테고리명", "confidence": 0.0~1.0 사이의 숫자, "reasoning": "분류 근거 한 문장" }

        영수증 정보:
        """;
}
