using MaterialSkin.Controls;

namespace App.Desktop.Forms;

partial class RegisterForm
{
    private System.ComponentModel.IContainer components = null!;

    private MaterialLabel lblTitle = null!;
    private MaterialTextBox2 txtDisplayName = null!;
    private MaterialTextBox2 txtEmail = null!;
    private MaterialTextBox2 txtPassword = null!;
    private MaterialTextBox2 txtPasswordConfirm = null!;
    private MaterialButton btnRegister = null!;
    private MaterialButton btnBack = null!;
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
        txtDisplayName = new MaterialTextBox2();
        txtEmail = new MaterialTextBox2();
        txtPassword = new MaterialTextBox2();
        txtPasswordConfirm = new MaterialTextBox2();
        btnRegister = new MaterialButton();
        btnBack = new MaterialButton();
        lblError = new MaterialLabel();

        SuspendLayout();

        // lblTitle
        lblTitle.AutoSize = true;
        lblTitle.Depth = 0;
        lblTitle.Font = new Font("Roboto", 20F, FontStyle.Bold);
        lblTitle.FontType = MaterialSkin.MaterialSkinManager.fontType.H5;
        lblTitle.Location = new Point(40, 100);
        lblTitle.Text = "회원가입";

        // txtDisplayName
        txtDisplayName.Depth = 0;
        txtDisplayName.Font = new Font("Roboto", 16F);
        txtDisplayName.Hint = "이름 (표시될 이름)";
        txtDisplayName.Location = new Point(40, 160);
        txtDisplayName.Size = new Size(340, 50);
        txtDisplayName.TabIndex = 0;

        // txtEmail
        txtEmail.Depth = 0;
        txtEmail.Font = new Font("Roboto", 16F);
        txtEmail.Hint = "이메일";
        txtEmail.Location = new Point(40, 220);
        txtEmail.Size = new Size(340, 50);
        txtEmail.TabIndex = 1;

        // txtPassword
        txtPassword.Depth = 0;
        txtPassword.Font = new Font("Roboto", 16F);
        txtPassword.Hint = "비밀번호 (6자 이상)";
        txtPassword.Location = new Point(40, 280);
        txtPassword.Size = new Size(340, 50);
        txtPassword.TabIndex = 2;
        txtPassword.UseSystemPasswordChar = true;

        // txtPasswordConfirm
        txtPasswordConfirm.Depth = 0;
        txtPasswordConfirm.Font = new Font("Roboto", 16F);
        txtPasswordConfirm.Hint = "비밀번호 확인";
        txtPasswordConfirm.Location = new Point(40, 340);
        txtPasswordConfirm.Size = new Size(340, 50);
        txtPasswordConfirm.TabIndex = 3;
        txtPasswordConfirm.UseSystemPasswordChar = true;

        // lblError
        lblError.AutoSize = false;
        lblError.Depth = 0;
        lblError.Font = new Font("Roboto", 10F);
        lblError.ForeColor = Color.FromArgb(211, 47, 47);
        lblError.Location = new Point(40, 398);
        lblError.Size = new Size(340, 20);
        lblError.Text = "";
        lblError.TextAlign = ContentAlignment.MiddleLeft;

        // btnRegister
        btnRegister.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnRegister.Density = MaterialButton.MaterialButtonDensity.Default;
        btnRegister.Depth = 0;
        btnRegister.HighEmphasis = true;
        btnRegister.Location = new Point(40, 428);
        btnRegister.Size = new Size(340, 36);
        btnRegister.TabIndex = 4;
        btnRegister.Text = "회원가입";
        btnRegister.Type = MaterialButton.MaterialButtonType.Contained;
        btnRegister.Click += BtnRegister_Click;

        // btnBack
        btnBack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnBack.Depth = 0;
        btnBack.HighEmphasis = false;
        btnBack.Location = new Point(40, 478);
        btnBack.Size = new Size(340, 36);
        btnBack.TabIndex = 5;
        btnBack.Text = "← 로그인으로 돌아가기";
        btnBack.Type = MaterialButton.MaterialButtonType.Text;
        btnBack.Click += BtnBack_Click;

        // RegisterForm
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(420, 560);
        Controls.Add(lblTitle);
        Controls.Add(txtDisplayName);
        Controls.Add(txtEmail);
        Controls.Add(txtPassword);
        Controls.Add(txtPasswordConfirm);
        Controls.Add(lblError);
        Controls.Add(btnRegister);
        Controls.Add(btnBack);
        Name = "RegisterForm";
        Text = "회원가입 - No More Receipts";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        ResumeLayout(false);
        PerformLayout();
    }
}
