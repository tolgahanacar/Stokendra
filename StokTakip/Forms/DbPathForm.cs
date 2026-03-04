using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class DbPathForm : Form
{
    public string SecilenYol { get; private set; } = "";

    private TextBox txtYol = new();
    private Button btnSec = new();
    private Button btnTamam = new();
    private Button btnIptal = new();

    public DbPathForm()
    {
        Text = L("db_location");
        Size = new Size(520, 180);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var lbl = new Label { Text = L("db_location_desc"), Left = 12, Top = 16, Width = 480, AutoSize = true };

        txtYol.Left = 12; txtYol.Top = 40; txtYol.Width = 400; txtYol.ReadOnly = true;

        btnSec.Text = L("browse"); btnSec.Left = 418; btnSec.Top = 38; btnSec.Width = 75;
        btnSec.Click += (_, _) =>
        {
            using var dlg = new SaveFileDialog
            {
                Title = L("db_location"),
                Filter = "SQLite DB|*.db",
                FileName = "stok.db"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                txtYol.Text = dlg.FileName;
        };

        btnTamam.Text = L("ok"); btnTamam.Left = 320; btnTamam.Top = 90; btnTamam.Width = 85;
        btnTamam.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(txtYol.Text)) { MessageBox.Show(L("no_location_selected")); return; }
            SecilenYol = txtYol.Text;
            DialogResult = DialogResult.OK;
        };

        btnIptal.Text = L("cancel"); btnIptal.Left = 415; btnIptal.Top = 90; btnIptal.Width = 78;
        btnIptal.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Controls.AddRange(new Control[] { lbl, txtYol, btnSec, btnTamam, btnIptal });
    }
}
