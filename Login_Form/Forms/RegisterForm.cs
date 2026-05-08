using App.Desktop.Services;
using MaterialSkin;
using MaterialSkin.Controls;

namespace App.Desktop.Forms;

public partial class RegisterForm : MaterialForm
{
    private readonly AuthApiClient _authApi;

    public RegisterForm(AuthApiClient authApi)
    {
        _authApi = authApi;
        MaterialSkinManager.Instance.AddFormToManage(this);
        InitializeComponent();
    }

    private async void BtnRegister_Click(object sender, EventArgs e)
    {
        // 클라이언트 입력 검증
        if (string.IsNullOrWhiteSpace(txtDisplayName.Text))
        {
            ShowError("이름을 입력하세요.");
            return;
        }
        if (string.IsNullOrWhiteSpace(txtEmail.Text) || !txtEmail.Text.Contains('@'))
        {
            ShowError("유효한 이메일을 입력하세요.");
            return;
        }
        if (txtPassword.Text.Length < 6)
        {
            ShowError("비밀번호는 6자 이상이어야 합니다.");
            return;
        }
        if (txtPassword.Text != txtPasswordConfirm.Text)
        {
            ShowError("비밀번호가 일치하지 않습니다.");
            return;
        }

        SetLoading(true);

        var (success, error, result) = await _authApi.RegisterAsync(
            txtEmail.Text.Trim(),
            txtPassword.Text,
            txtDisplayName.Text.Trim());

        SetLoading(false);

        if (!success || result is null)
        {
            ShowError(error ?? "회원가입에 실패했습니다.");
            return;
        }

        MessageBox.Show(
            $"회원가입이 완료되었습니다!\n\n{result.DisplayName}님, 환영합니다.\n로그인 화면으로 이동합니다.",
            "회원가입 성공",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        this.Close(); // 닫으면 LoginForm으로 돌아감
    }

    private void BtnBack_Click(object sender, EventArgs e)
    {
        this.Close();
    }

    private void ShowError(string message) => lblError.Text = message;
    private void ClearError() => lblError.Text = "";

    private void SetLoading(bool isLoading)
    {
        btnRegister.Enabled = !isLoading;
        btnBack.Enabled = !isLoading;
        btnRegister.Text = isLoading ? "처리 중..." : "회원가입";
        ClearError();
    }
}
