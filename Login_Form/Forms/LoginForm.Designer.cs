using MaterialSkin.Controls;

namespace App.Desktop.Forms;

partial class LoginForm
{
    private System.ComponentModel.IContainer components = null!;

    // MaterialSkin 컨트롤들
    private MaterialLabel lblTitle = null!;
    private MaterialLabel lblSubtitle = null!;
    private MaterialTextBox2 txtEmail = null!;
    private MaterialTextBox2 txtPassword = null!;
    private MaterialButton btnLogin = null!;
    private MaterialButton btnGoRegister = null!;
    private MaterialLabel lblError = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        lblTitle = new MaterialLabel();
        lblSubtitle = new MaterialLabel();
        txtEmail = new MaterialTextBox2();
        txtPassword = new MaterialTextBox2();
        btnLogin = new MaterialButton();
        btnGoRegister = new MaterialButton();
        lblError = new MaterialLabel();

        SuspendLayout();

        // lblTitle
        lblTitle.AutoSize = true;
        lblTitle.Depth = 0;
        lblTitle.Font = new Font("Roboto", 24F, FontStyle.Bold);
        lblTitle.FontType = MaterialSkin.MaterialSkinManager.fontType.H4;
        lblTitle.Location = new Point(40, 100);
        lblTitle.Text = "No More Receipts";

        // lblSubtitle
        lblSubtitle.AutoSize = true;
        lblSubtitle.Depth = 0;
        lblSubtitle.Font = new Font("Roboto", 11F);
        lblSubtitle.FontType = MaterialSkin.MaterialSkinManager.fontType.Subtitle1;
        lblSubtitle.Location = new Point(40, 145);
        lblSubtitle.Text = "로그인하여 영수증 관리를 시작하세요";

        // txtEmail
        txtEmail.Depth = 0;
        txtEmail.Font = new Font("Roboto", 16F);
        txtEmail.Hint = "이메일";
        txtEmail.Location = new Point(40, 210);
        txtEmail.Size = new Size(340, 50);
        txtEmail.TabIndex = 0;

        // txtPassword
        txtPassword.Depth = 0;
        txtPassword.Font = new Font("Roboto", 16F);
        txtPassword.Hint = "비밀번호";
        txtPassword.Location = new Point(40, 275);
        txtPassword.Size = new Size(340, 50);
        txtPassword.TabIndex = 1;
        txtPassword.UseSystemPasswordChar = true;

        // lblError - 에러 메시지 표시용, 기본은 숨김
        lblError.AutoSize = false;
        lblError.Depth = 0;
        lblError.Font = new Font("Roboto", 10F);
        lblError.ForeColor = Color.FromArgb(211, 47, 47); // Material Red 700
        lblError.Location = new Point(40, 335);
        lblError.Size = new Size(340, 20);
        lblError.Text = "";
        lblError.TextAlign = ContentAlignment.MiddleLeft;

        // btnLogin
        btnLogin.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnLogin.Density = MaterialButton.MaterialButtonDensity.Default;
        btnLogin.Depth = 0;
        btnLogin.HighEmphasis = true;
        btnLogin.Location = new Point(40, 365);
        btnLogin.Size = new Size(340, 36);
        btnLogin.TabIndex = 2;
        btnLogin.Text = "로그인";
        btnLogin.Type = MaterialButton.MaterialButtonType.Contained;
        btnLogin.Click += BtnLogin_Click;

        // btnGoRegister
        btnGoRegister.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnGoRegister.Depth = 0;
        btnGoRegister.HighEmphasis = false;
        btnGoRegister.Location = new Point(40, 415);
        btnGoRegister.Size = new Size(340, 36);
        btnGoRegister.TabIndex = 3;
        btnGoRegister.Text = "계정이 없으신가요? 회원가입";
        btnGoRegister.Type = MaterialButton.MaterialButtonType.Text;
        btnGoRegister.Click += BtnGoRegister_Click;

        // LoginForm 자체 설정
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(420, 500);
        Controls.Add(lblTitle);
        Controls.Add(lblSubtitle);
        Controls.Add(txtEmail);
        Controls.Add(txtPassword);
        Controls.Add(lblError);
        Controls.Add(btnLogin);
        Controls.Add(btnGoRegister);
        Name = "LoginForm";
        Text = "로그인 - No More Receipts";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        ResumeLayout(false);
        PerformLayout();
    }
}
