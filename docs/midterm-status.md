# 중간 발표 구현 현황

## 완료된 내용

### AI 오케스트레이터 (강현서)
- AI 추천/확정/정확도/대시보드 API 구현
- AI 데모 체크리스트 API 구현
- AI 샘플 데이터 생성/초기화 API 구현
- Semantic Kernel 1.74 + GPT-4o 연동

### Gateway (강현서)
- YARP 2.3.0 리버스 프록시 설정
- `/api/proxy/analysis/**` → `/api/analysis/**` 라우팅
- Gateway 상태 조회 API 구현

### Analysis (권건우 스캐폴딩)
- 카테고리별 지출 집계 API (LINQ GroupBy)
- 월별 지출 추이 API
- Analysis 서비스 상태/체크리스트 API

### Auth/JWT (남건희 스캐폴딩)
- 회원가입 (`POST /api/auth/register`)
- 로그인 + JWT 발급 (`POST /api/auth/login`)
- 내 정보 조회 (`GET /api/auth/me`, 인증 필요)
- PBKDF2 비밀번호 해싱, HS256 JWT 서명

### Receipt/OCR (선현진 스캐폴딩)
- 영수증 업로드 + OCR 텍스트 파싱 (`POST /api/receipts/upload`)
- 영수증 목록 조회 (`GET /api/receipts/`, 페이지네이션)
- 카테고리 확정 (`POST /api/receipts/{id}/confirm`)
- OCR 스텁: 상호명/금액/날짜 정규식 파싱
- AI 카테고리 자동 추천 파이프라인 연동

### 인프라 (강현서)
- EF Core Code-First: AiInferenceLog + User + Receipt 테이블 (V1, V2 마이그레이션)
- CI/CD GitHub Actions (feat/**, master 브랜치)
- Health Check 엔드포인트
- 앱 부팅 안정화

## 아직 남은 내용 (11주차 이후)
- gRPC 기반 서비스 간 통신 (11~12주차)
- OpenTelemetry/Jaeger 분산 추적 (14주차)
- WinForms 기반 실제 시각화 화면 (권건우 담당)
- 외부 OCR API(Azure AI Vision) 실제 연동 (선현진 담당)
- Receipt/OCR 이미지 업로드 처리기 (선현진 담당)

## 발표 시연 흐름
1. Health Check로 전체 서비스 상태 확인 (`GET /api/health`)
2. Auth 회원가입 → 로그인 → JWT 토큰 발급
3. 영수증 텍스트 업로드 → OCR 파싱 + AI 카테고리 추천 확인
4. 영수증 카테고리 사용자 확정
5. AI 대시보드 요약 확인 (정확도/대기건수/최근로그)
6. Analysis 월별 추이 및 카테고리 집계 확인
7. Gateway 프록시 경로로 Analysis API 재호출 시연
8. 데모 시드/리셋으로 반복 시연 가능

## 참고
중간 발표용 시연 안정성 우선. 실제 외부 서비스 연동(OCR API, gRPC)과 WinForms UI는 후속 단계.