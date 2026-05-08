using App.Desktop.Services;
using MaterialSkin;
using MaterialSkin.Controls;

namespace App.Desktop.Forms;

/// <summary>
/// 로그인 화면입니다.
/// MaterialSkin.2를 적용한 MaterialForm을 상속합니다.
/// 
/// [기존 WinForms와의 차이점]
/// - Form 대신 MaterialForm 상속
/// - Button 대신 MaterialButton 사용
/// - TextBox 대신 MaterialTextBox2 사용
/// - Label 대신 MaterialLabel 사용
/// - MaterialSkinManager로 테마/색상 전역 관리
/// </summary>
public partial class LoginForm : MaterialForm
{
    private readonly AuthApiClient _authApi;

    public LoginForm(AuthApiClient authApi)
    {
        _authApi = authApi;

        // MaterialSkinManager는 Program.cs에서 이미 초기화되어 있으므로
        // 여기서는 AddFormToManage만 호출합니다.
        MaterialSkinManager.Instance.AddFormToManage(this);

        InitializeComponent();
    }

    // ──────────────────────────────────────────────
    // 이벤트 핸들러
    // ──────────────────────────────────────────────

    private async void BtnLogin_Click(object sender, EventArgs e)
    {
        // 간단한 입력 검증 (서버에서도 검증하지만 UX를 위해 클라이언트에서도)
        if (string.IsNullOrWhiteSpace(txtEmail.Text))
        {
            ShowError("이메일을 입력하세요.");
            return;
        }
        if (string.IsNullOrWhiteSpace(txtPassword.Text))
        {
            ShowError("비밀번호를 입력하세요.");
            return;
        }

        SetLoading(true);

        var (success, error, result) = await _authApi.LoginAsync(
            txtEmail.Text.Trim(),
            txtPassword.Text);

        SetLoading(false);

        if (!success || result is null)
        {
            ShowError(error ?? "로그인에 실패했습니다.");
            return;
        }

        // JWT 토큰과 사용자 정보를 세션에 저장
        SessionManager.Current.SetSession(
            result.AccessToken,
            result.Email,
            result.DisplayName,
            result.Role);

        // 로그인 성공 → 메인 폼으로 이동
        // MainForm은 건우 담당이므로, 빌드 오류 방지를 위해 일단 주석 처리
        // 건우가 MainForm을 만들면 아래 주석을 해제하세요.
        // var mainForm = new Forms.MainForm();
        // mainForm.Show();
        // this.Hide();

        // 임시: 로그인 성공 메시지만 표시
        MessageBox.Show(
            $"로그인 성공!\n\n환영합니다, {result.DisplayName}님",
            "로그인 성공",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void BtnGoRegister_Click(object sender, EventArgs e)
    {
        var registerForm = new RegisterForm(_authApi);
        registerForm.Show();
        this.Hide();

        // 회원가입 창이 닫히면 로그인 창 다시 표시
        registerForm.FormClosed += (_, _) => this.Show();
    }

    // ──────────────────────────────────────────────
    // 헬퍼 메서드
    // ──────────────────────────────────────────────

    private void ShowError(string message)
    {
        lblError.Text = message;
    }

    private void ClearError()
    {
        lblError.Text = "";
    }

    /// <summary>
    /// API 호출 중 버튼 비활성화 처리
    /// </summary>
    private void SetLoading(bool isLoading)
    {
        btnLogin.Enabled = !isLoading;
        btnGoRegister.Enabled = !isLoading;
        btnLogin.Text = isLoading ? "로그인 중..." : "로그인";
        ClearError();
    }
}
