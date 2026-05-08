using App.Desktop.Forms;
using App.Desktop.Services;
using MaterialSkin;

// ──────────────────────────────────────────────────────────────
// WinForms 앱 진입점
// ──────────────────────────────────────────────────────────────
ApplicationConfiguration.Initialize();

// ── 1. MaterialSkin 전역 설정 ──────────────────────────────────
// 기존 WinForms와의 차이점:
//   - MaterialSkinManager.Instance를 통해 테마를 전역으로 설정
//   - 각 Form은 MaterialForm을 상속하고 AddFormToManage를 호출해야 함
var materialManager = MaterialSkinManager.Instance;
materialManager.EnforceBackcolorOnAllComponents = true;

// 라이트 / 다크 모드 선택
materialManager.Theme = MaterialSkinManager.Themes.LIGHT;

// Primary, Dark, Accent 색상 설정 (Material Design 색상 팔레트 사용)
// 아래는 Blue 테마 - 원하는 색상으로 변경 가능
materialManager.ColorScheme = new ColorScheme(
    Primary.Blue600,    // 주 색상 (앱바, 버튼 등)
    Primary.Blue800,    // 주 색상 (어두운 버전)
    Primary.Blue200,    // 주 색상 (밝은 버전)
    Accent.LightBlue200, // 강조 색상
    TextShade.WHITE      // 앱바 텍스트 색상
);

// ── 2. HttpClient 설정 ────────────────────────────────────────
// 백엔드 주소 (appsettings.json 연동 없이 직접 지정)
// 백엔드 실행 후 실제 포트에 맞게 수정하세요 (launchSettings.json 기준: 65289)
var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://localhost:65289"),
    Timeout = TimeSpan.FromSeconds(30)
};

// ── 3. 서비스 생성 ─────────────────────────────────────────────
var authApiClient = new AuthApiClient(httpClient);

// ── 4. 앱 시작 ────────────────────────────────────────────────
Application.Run(new LoginForm(authApiClient));
