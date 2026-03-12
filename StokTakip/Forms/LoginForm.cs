using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class LoginForm : Form
{
    private TextBox txtUser = new(), txtPass = new();
    private Label lblError = new();
    private int _attempts = 0;

    public LoginForm()
    {
        Text = "Stokendra \u2014 Giri\u015f";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        Size = new Size(420, 380);
        BackColor = UIHelper.BgDark;

        // Brand header
        var pnlTop = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = Color.FromArgb(18, 24, 38) };
        var lblLogo = new Label
        {
            Text = "S",
            Font = new Font("Segoe UI", 28, FontStyle.Bold),
            ForeColor = UIHelper.AccentCyan,
            BackColor = Color.FromArgb(30, 50, 80),
            Width = 52, Height = 52, Left = 176, Top = 10,
            TextAlign = ContentAlignment.MiddleCenter
        };
        lblLogo.Region = new Region(GlowCard.RoundedRect(new Rectangle(0, 0, 52, 52), 26)); 
        var lblBrand = new Label { Text = "Stokendra", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = UIHelper.TextWhite, AutoSize = true, Left = 135, Top = 66 };
        pnlTop.Controls.AddRange(new Control[] { lblLogo, lblBrand });

        // Form body
        var pnlBody = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.BgDark, Padding = new Padding(50, 20, 50, 20) };

        var lblUser = new Label { Text = L("username"), Font = UIHelper.FontLabel, ForeColor = UIHelper.TextSecondary, Left = 50, Top = 10, AutoSize = true };
        txtUser = new TextBox { Left = 50, Top = 32, Width = 300, Font = new Font("Segoe UI", 11) };
        UIHelper.StyleTextBox(txtUser); txtUser.Text = "admin";

        var lblPass = new Label { Text = L("password"), Font = UIHelper.FontLabel, ForeColor = UIHelper.TextSecondary, Left = 50, Top = 72, AutoSize = true };
        txtPass = new TextBox { Left = 50, Top = 94, Width = 300, Font = new Font("Segoe UI", 11), UseSystemPasswordChar = true };
        UIHelper.StyleTextBox(txtPass);

        lblError = new Label { Text = "", Left = 50, Top = 135, Width = 300, Height = 20, Font = new Font("Segoe UI", 8.5f), ForeColor = UIHelper.StokWarning, TextAlign = ContentAlignment.MiddleCenter };

        var btnLogin = UIHelper.MakeButton("🔑 " + L("login_btn"), UIHelper.AccentCyan, 50, 160, 300, 42);
        btnLogin.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        btnLogin.Click += Login;

        var lblCopy = new Label { Text = "\u00a9 2026 Tolgahan Acar. T\u00fcm haklar\u0131 sakl\u0131d\u0131r.", Left = 50, Top = 215, Width = 300, Font = new Font("Segoe UI", 7.5f), ForeColor = UIHelper.TextDim, TextAlign = ContentAlignment.MiddleCenter };

        pnlBody.Controls.AddRange(new Control[] { lblUser, txtUser, lblPass, txtPass, lblError, btnLogin, lblCopy });

        Controls.Add(pnlBody); Controls.Add(pnlTop);

        AcceptButton = btnLogin;
        txtPass.Focus();
    }

    private async void Login(object? s, EventArgs e)
    {
        string user = txtUser.Text.Trim(), pass = txtPass.Text;
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        { lblError.Text = L("login_failed"); return; }

        if (Program.DB!.KullaniciDogrula(user, pass))
        {
            Program.CurrentUser = user;
            DialogResult = DialogResult.OK;
        }
        else
        {
            _attempts++;
            lblError.Text = L("login_failed");
            if (_attempts >= 3)
            {
                lblError.Text += " (bekleniyor...)";
                Enabled = false;
                await Task.Delay(3000);
                Enabled = true;
                lblError.Text = L("login_failed");
                _attempts = 0;
            }
        }
    }
}
