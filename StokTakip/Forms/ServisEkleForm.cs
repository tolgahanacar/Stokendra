using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class ServisEkleForm : Form
{
    public ServisKaydi Kayit { get; private set; }

    private TextBox txtCihazAdi = new();
    private TextBox txtSeriNumarasi = new();
    private DateTimePicker dtBakimTarihi = new();
    private TextBox txtAciklama = new();

    public ServisEkleForm(ServisKaydi? kayit = null)
    {
        Kayit = kayit ?? new ServisKaydi { BakimTarihi = DateTime.Now };
        Text = kayit == null ? L("new_service_record", "Yeni Servis Kaydı") : L("edit_service_record", "Servis Kaydı Düzenle");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; BackColor = UIHelper.BgPanel;

        var strip = new Panel { Height = 44, BackColor = Color.FromArgb(30, 37, 52), Dock = DockStyle.Top };
        strip.Controls.Add(new Label { Text = Text, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = UIHelper.AccentBlue, Left = 16, Top = 10, AutoSize = true });

        var tbl = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16, 8, 16, 8),
            BackColor = UIHelper.BgPanel, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        // Cihaz Adı
        tbl.Controls.Add(MakeLabel(L("device_name_required", "Cihaz Adı *")), 0, row);
        txtCihazAdi = MakeTextBox(); tbl.Controls.Add(txtCihazAdi, 1, row); row++;

        // Seri Numarası
        tbl.Controls.Add(MakeLabel(L("serial_number", "Seri Numarası")), 0, row);
        txtSeriNumarasi = MakeTextBox(); tbl.Controls.Add(txtSeriNumarasi, 1, row); row++;

        // Bakım Tarihi
        tbl.Controls.Add(MakeLabel(L("maintenance_date", "Bakım Tarihi *")), 0, row);
        dtBakimTarihi = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Short };
        UIHelper.StyleDatePicker(dtBakimTarihi);
        tbl.Controls.Add(dtBakimTarihi, 1, row); row++;

        // Açıklama
        tbl.Controls.Add(MakeLabel(L("description", "Açıklama")), 0, row);
        txtAciklama = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 60, MaxLength = 500 };
        UIHelper.StyleTextBox(txtAciklama);
        tbl.Controls.Add(txtAciklama, 1, row); row++;

        // Buttons
        var pnlBtn = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Height = 40, Padding = new Padding(0, 4, 0, 0) };
        var btnI = UIHelper.MakeButton(L("cancel"), UIHelper.BgInput, 0, 0, 90, 32); btnI.Dock = DockStyle.None;
        var btnK = UIHelper.MakeButton(L("save"), UIHelper.AccentBlue, 0, 0, 100, 32); btnK.Dock = DockStyle.None;
        btnK.Click += BtnSave_Click; btnI.Click += (_, _) => DialogResult = DialogResult.Cancel;
        pnlBtn.Controls.AddRange(new Control[] { btnI, btnK });
        tbl.Controls.Add(new Label(), 0, row);
        tbl.Controls.Add(pnlBtn, 1, row);

        Controls.Add(tbl);
        Controls.Add(strip);
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; MinimumSize = new Size(420, 100);

        LoadData();
    }

    static Label MakeLabel(string t) => new Label { Text = t, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9), ForeColor = UIHelper.TextSecondary, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 4, 8, 4) };
    static TextBox MakeTextBox() { var t = new TextBox { Dock = DockStyle.Fill }; UIHelper.StyleTextBox(t); return t; }

    private void LoadData()
    {
        txtCihazAdi.Text = Kayit.CihazAdi;
        txtSeriNumarasi.Text = Kayit.SeriNumarasi;
        if (Kayit.BakimTarihi >= dtBakimTarihi.MinDate && Kayit.BakimTarihi <= dtBakimTarihi.MaxDate)
            dtBakimTarihi.Value = Kayit.BakimTarihi;
        txtAciklama.Text = Kayit.Aciklama;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtCihazAdi.Text))
        {
            MessageBox.Show(L("device_name_required", "Cihaz adı zorunludur."), L("warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Kayit.CihazAdi = txtCihazAdi.Text.Trim();
        Kayit.SeriNumarasi = txtSeriNumarasi.Text.Trim();
        Kayit.BakimTarihi = dtBakimTarihi.Value;
        Kayit.Aciklama = txtAciklama.Text.Trim();

        DialogResult = DialogResult.OK;
    }
}
