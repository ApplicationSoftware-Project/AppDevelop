-- =====================================================
-- 영수증 관리 시스템 Mock Data 시드 스크립트
-- 대상 DB: ReceiptManager (PostgreSQL 16, Npgsql/EF Core)
-- 사용법: DBeaver에서 ReceiptManager DB 접속 → SQL Editor → 전체 실행
-- 주의: 컬럼/테이블명이 PascalCase이므로 반드시 큰따옴표(") 필요
-- =====================================================

-- -----------------------------------------------------
-- (선택) 기존 데이터 초기화: 필요할 때만 아래 주석 해제
-- -----------------------------------------------------
-- TRUNCATE TABLE "AiInferenceLogs", "Receipts", "Users" RESTART IDENTITY CASCADE;


-- -----------------------------------------------------
-- 1. 사용자 (3명, 고정 UUID로 재실행 안전)
-- -----------------------------------------------------
INSERT INTO "Users" (
    "Id", "Email", "PasswordHash", "DisplayName", "Role",
    "PhoneNumber", "EmailNotification", "PushNotification",
    "CreatedAt", "LastLoginAt"
)
VALUES
(
    '11111111-1111-1111-1111-111111111111'::uuid,
    'alice@example.com',
    '$2a$11$dummyhashplaceholderdummyhashplaceholderdummyhashpla',
    'Alice 김',
    'User',
    '010-1234-5678',
    TRUE, TRUE,
    NOW() - INTERVAL '300 days',
    NOW() - INTERVAL '1 day'
),
(
    '22222222-2222-2222-2222-222222222222'::uuid,
    'bob@example.com',
    '$2a$11$dummyhashplaceholderdummyhashplaceholderdummyhashpla',
    'Bob 이',
    'User',
    '010-2345-6789',
    TRUE, FALSE,
    NOW() - INTERVAL '200 days',
    NOW() - INTERVAL '5 days'
),
(
    '33333333-3333-3333-3333-333333333333'::uuid,
    'admin@example.com',
    '$2a$11$dummyhashplaceholderdummyhashplaceholderdummyhashpla',
    '관리자',
    'Admin',
    '010-9999-0000',
    TRUE, TRUE,
    NOW() - INTERVAL '365 days',
    NOW()
)
ON CONFLICT ("Id") DO NOTHING;


-- -----------------------------------------------------
-- 2. 영수증 (150건, 지난 12개월에 걸쳐 분포)
--    카테고리/매장명을 패턴으로 생성하여 분석 통계가 의미 있게 나오도록 함
-- -----------------------------------------------------
INSERT INTO "Receipts" (
    "Id", "UserId", "StoreName", "Amount", "PurchasedAt",
    "ImagePath", "ContentType", "RawOcrText",
    "Category", "AiSuggestedCategory", "AiLogId",
    "Status", "CreatedAt", "ProcessedAt", "UpdatedAt"
)
SELECT
    gen_random_uuid(),

    -- UserId: 3명 라운드로빈
    (ARRAY[
        '11111111-1111-1111-1111-111111111111'::uuid,
        '22222222-2222-2222-2222-222222222222'::uuid,
        '33333333-3333-3333-3333-333333333333'::uuid
    ])[((g - 1) % 3) + 1] AS "UserId",

    -- StoreName: 카테고리별 매장 풀
    CASE (g % 8)
        WHEN 0 THEN (ARRAY['김밥천국','맥도날드','BBQ치킨','본죽','한솥도시락'])[((g / 8) % 5) + 1]
        WHEN 1 THEN (ARRAY['스타벅스','이디야','투썸플레이스','커피빈','메가커피'])[((g / 8) % 5) + 1]
        WHEN 2 THEN (ARRAY['카카오T','서울교통공사','쏘카','코레일'])[((g / 8) % 4) + 1]
        WHEN 3 THEN (ARRAY['쿠팡','11번가','무신사','올리브영'])[((g / 8) % 4) + 1]
        WHEN 4 THEN (ARRAY['CGV','메가박스','YES24','교보문고'])[((g / 8) % 4) + 1]
        WHEN 5 THEN (ARRAY['약국','내과의원','치과','한의원'])[((g / 8) % 4) + 1]
        WHEN 6 THEN (ARRAY['SKT','KT','LG U+','알뜰폰'])[((g / 8) % 4) + 1]
        ELSE        (ARRAY['이마트','롯데마트','홈플러스','GS25'])[((g / 8) % 4) + 1]
    END AS "StoreName",

    -- Amount: 1,500 ~ 250,000원 사이
    ROUND((1500 + random() * 248500)::numeric, 2) AS "Amount",

    -- PurchasedAt: 지난 365일 내 임의 시점
    NOW() - make_interval(days => (random() * 365)::int,
                          hours => (random() * 24)::int) AS "PurchasedAt",

    -- ImagePath / ContentType
    '/uploads/receipts/' || gen_random_uuid() || '.jpg' AS "ImagePath",
    'image/jpeg' AS "ContentType",
    NULL AS "RawOcrText",

    -- Category: 사용자가 확정한 카테고리
    CASE (g % 8)
        WHEN 0 THEN '식비'
        WHEN 1 THEN '카페'
        WHEN 2 THEN '교통'
        WHEN 3 THEN '쇼핑'
        WHEN 4 THEN '여가'
        WHEN 5 THEN '의료'
        WHEN 6 THEN '통신'
        ELSE        '생활'
    END AS "Category",

    -- AiSuggestedCategory: 85% 확률로 정답, 15%는 다른 카테고리
    CASE WHEN random() < 0.85
         THEN CASE (g % 8)
                  WHEN 0 THEN '식비'  WHEN 1 THEN '카페'  WHEN 2 THEN '교통'
                  WHEN 3 THEN '쇼핑'  WHEN 4 THEN '여가'  WHEN 5 THEN '의료'
                  WHEN 6 THEN '통신'  ELSE        '생활'
              END
         ELSE (ARRAY['기타','미분류','기타지출'])[1 + (FLOOR(random() * 3))::int]
    END AS "AiSuggestedCategory",

    NULL AS "AiLogId",

    -- Status: 70% Confirmed, 15% AiCategorized, 10% OcrProcessed, 5% Pending
    -- _r을 한 번만 계산해 각 WHEN이 같은 난수를 참조하도록 함
    CASE
        WHEN _r < 0.70 THEN 'Confirmed'
        WHEN _r < 0.85 THEN 'AiCategorized'
        WHEN _r < 0.95 THEN 'OcrProcessed'
        ELSE 'Pending'
    END AS "Status",

    NOW() - make_interval(days => (random() * 365)::int) AS "CreatedAt",
    NOW() - make_interval(days => (random() * 360)::int) AS "ProcessedAt",
    NULL AS "UpdatedAt"

FROM (SELECT g, random() AS _r FROM generate_series(1, 150) AS g) t;


-- -----------------------------------------------------
-- 3. AI 추론 로그 (Confirmed/AiCategorized 영수증 중 100건)
--    AnalysisService 외에도 AiAccuracyService 등 다른 기능 테스트에 활용
-- -----------------------------------------------------
INSERT INTO "AiInferenceLogs" (
    "Id", "ReceiptId", "SuggestedCategory", "Confidence",
    "FinalCategory", "IsCorrect", "CreatedAt", "UpdatedAt"
)
SELECT
    gen_random_uuid(),
    r."Id",
    COALESCE(r."AiSuggestedCategory", '기타'),
    -- Confidence: 0.55 ~ 0.99 (precision 5,4)
    ROUND((0.55 + random() * 0.44)::numeric, 4)::double precision,
    r."Category",
    -- IsCorrect: AI 제안과 최종 카테고리가 같은지
    (r."Category" = r."AiSuggestedCategory"),
    r."CreatedAt",
    CASE WHEN random() < 0.6
         THEN r."CreatedAt" + INTERVAL '1 hour'
         ELSE NULL
    END
FROM "Receipts" r
WHERE r."Status" IN ('Confirmed', 'AiCategorized')
ORDER BY random()
LIMIT 100;


-- -----------------------------------------------------
-- 4. Receipt.AiLogId 역참조 연결
--    AiInferenceLogs 삽입 후 각 영수증의 AiLogId를 업데이트
-- -----------------------------------------------------
UPDATE "Receipts" r
SET    "AiLogId" = l."Id"
FROM   "AiInferenceLogs" l
WHERE  l."ReceiptId" = r."Id";


-- -----------------------------------------------------
-- 5. 결과 검증 (실행 후 확인용)
-- -----------------------------------------------------
SELECT 'Users' AS table_name, COUNT(*) AS row_count FROM "Users"
UNION ALL
SELECT 'Receipts',        COUNT(*) FROM "Receipts"
UNION ALL
SELECT 'AiInferenceLogs', COUNT(*) FROM "AiInferenceLogs";

-- 월별 추이 확인 (Analysis API 결과 미리보기)
SELECT
    EXTRACT(YEAR  FROM "PurchasedAt")::int AS year,
    EXTRACT(MONTH FROM "PurchasedAt")::int AS month,
    COUNT(*)            AS total_count,
    SUM("Amount")::numeric(18,2) AS total_amount
FROM "Receipts"
WHERE "PurchasedAt" IS NOT NULL AND "Amount" IS NOT NULL
GROUP BY 1, 2
ORDER BY 1, 2;

-- 카테고리별 지출 합계
SELECT
    "Category",
    COUNT(*) AS total_count,
    SUM("Amount")::numeric(18,2) AS total_amount
FROM "Receipts"
WHERE "Category" IS NOT NULL AND "Amount" IS NOT NULL
GROUP BY "Category"
ORDER BY total_amount DESC;
