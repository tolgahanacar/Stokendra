using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class StokKartiDuzenleForm : Form
{
    private TextBox txtAd = new(), txtKodNo = new(), txtAciklama = new(), txtMinStok = new();
    private ComboBox cmbKategori = new(), cmbKartTipi = new(), cmbUstKart = new();
    private Label lblUstKart = new(), lblMinStok = new();
    private readonly StokKarti? _mevcut;
    private List<StokKarti> _ustKartlar = new();

    public StokKartiDuzenleForm(StokKarti? kart)
    {
        _mevcut = kart;
        Text = kart == null ? L("new_stock_card") : L("edit_stock_card");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; BackColor = UIHelper.BgPanel;

        // ── TableLayoutPanel ile esnek form ──
        var tbl = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 9,
            Padding = new Padding(16, 8, 16, 8),
            BackColor = UIHelper.BgPanel,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        // Header strip
        var strip = new Panel { Height = 44, BackColor = Color.FromArgb(30, 37, 52), Dock = DockStyle.Top };
        strip.Controls.Add(new Label { Text = kart == null ? L("new_stock_card") : L("edit_stock_card"),
            Font = new Font("SF Pro Display", 11, FontStyle.Bold), ForeColor = UIHelper.AccentBlue, Left = 16, Top = 10, AutoSize = true });

        // Kart Tipi
        tbl.Controls.Add(MakeLabel(L("card_type_label")), 0, row);
        cmbKartTipi = MakeCombo(new[] { L("child_card"), L("parent_card") }, 0);
        cmbKartTipi.SelectedIndexChanged += (_, _) => KartTipiDegisti();
        tbl.Controls.Add(cmbKartTipi, 1, row); row++;

        // Üst Kart
        lblUstKart = MakeLabel(L("parent_card_label"));
        tbl.Controls.Add(lblUstKart, 0, row);
        cmbUstKart = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        UIHelper.StyleComboBox(cmbUstKart);
        _ustKartlar = Program.DB!.UstKartlariGetir();
        cmbUstKart.Items.Add(L("no_parent"));
        foreach (var uk in _ustKartlar) cmbUstKart.Items.Add(uk);
        cmbUstKart.SelectedIndex = 0;
        tbl.Controls.Add(cmbUstKart, 1, row); row++;

        // Stok Adı
        tbl.Controls.Add(MakeLabel(L("stock_name_required")), 0, row);
        txtAd = MakeTextBox(); tbl.Controls.Add(txtAd, 1, row); row++;

        // Stok Kodu (oto)
        tbl.Controls.Add(MakeLabel(L("code_no_auto")), 0, row);
        txtKodNo = MakeTextBox(); txtKodNo.ForeColor = UIHelper.AccentCyan; txtKodNo.Width = 120;
        if (kart == null) txtKodNo.Text = Program.DB!.SonrakiStokKodu();
        tbl.Controls.Add(txtKodNo, 1, row); row++;

        // Kategori
        tbl.Controls.Add(MakeLabel(L("category")), 0, row);
        cmbKategori = MakeCombo(new[] { L("cat_printer"), L("cat_toner"), L("cat_spare"), L("cat_drum", "Drum Ünitesi"), L("cat_other") }, 3);
        tbl.Controls.Add(cmbKategori, 1, row); row++;

        // Min Stok
        lblMinStok = MakeLabel(L("min_stock"));
        tbl.Controls.Add(lblMinStok, 0, row);
        txtMinStok = MakeTextBox(); txtMinStok.Text = "0"; txtMinStok.Width = 80;
        tbl.Controls.Add(txtMinStok, 1, row); row++;

        // Açıklama
        tbl.Controls.Add(MakeLabel(L("description_label")), 0, row);
        txtAciklama = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 55, MaxLength = 500,
            BackColor = UIHelper.BgInput, ForeColor = UIHelper.TextPrimary, BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("SF Pro Text", 9.5f) };
        tbl.Controls.Add(txtAciklama, 1, row); row++;

        // Butonlar
        var pnlBtn = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Height = 40, Padding = new Padding(0, 4, 0, 0) };
        var btnI = UIHelper.MakeButton(L("cancel"), UIHelper.BtnBarBg, 0, 0, 90, 32); btnI.Dock = DockStyle.None;
        var btnK = UIHelper.MakeButton(L("save"), UIHelper.AccentBlue, 0, 0, 100, 32); btnK.Dock = DockStyle.None;
        btnK.Click += Kaydet; btnI.Click += (_, _) => DialogResult = DialogResult.Cancel;
        pnlBtn.Controls.AddRange(new Control[] { btnI, btnK });
        tbl.Controls.Add(new Label(), 0, row); // spacer
        tbl.Controls.Add(pnlBtn, 1, row);

        // Mevcut verileri yükle
        if (kart != null)
        {
            txtAd.Text = kart.Ad; txtKodNo.Text = kart.KodNo; txtAciklama.Text = kart.Aciklama;
            txtMinStok.Text = kart.MinStok.ToString();
            cmbKartTipi.SelectedIndex = kart.KartTipi == "Ust" ? 1 : 0;
            int ci = -1; for (int i = 0; i < cmbKategori.Items.Count; i++) if (cmbKategori.Items[i]?.ToString() == kart.Kategori) { ci = i; break; }
            if (ci >= 0) cmbKategori.SelectedIndex = ci;
            if (kart.UstKartId.HasValue) { int ui = _ustKartlar.FindIndex(u => u.Id == kart.UstKartId.Value); if (ui >= 0) cmbUstKart.SelectedIndex = ui + 1; }
        }
        KartTipiDegisti();

        Controls.Add(tbl);
        Controls.Add(strip);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(480, 100);
    }

    static Label MakeLabel(string t) => new Label { Text = t, Dock = DockStyle.Fill, Font = new Font("SF Pro Text", 9), ForeColor = UIHelper.TextSecondary, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 4, 8, 4) };
    static TextBox MakeTextBox() { var t = new TextBox { Dock = DockStyle.Fill }; UIHelper.StyleTextBox(t); return t; }
    static ComboBox MakeCombo(string[] items, int sel) { var c = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList }; UIHelper.StyleComboBox(c); c.Items.AddRange(items); if (sel >= 0 && sel < items.Length) c.SelectedIndex = sel; return c; }

    private void KartTipiDegisti() { bool alt = cmbKartTipi.SelectedIndex == 0; lblUstKart.Visible = cmbUstKart.Visible = alt; lblMinStok.Visible = txtMinStok.Visible = alt; }

    private void Kaydet(object? s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtAd.Text)) { MessageBox.Show(L("name_required")); return; }
        if (string.IsNullOrWhiteSpace(txtKodNo.Text)) txtKodNo.Text = Program.DB!.SonrakiStokKodu();
        int minStok = 0;
        if (txtMinStok.Visible && !string.IsNullOrWhiteSpace(txtMinStok.Text) && !int.TryParse(txtMinStok.Text, out minStok))
        { MessageBox.Show(L("min_stock_invalid")); return; }
        string kartTipi = cmbKartTipi.SelectedIndex == 1 ? "Ust" : "Alt";
        int? ustKartId = null;
        if (kartTipi == "Alt" && cmbUstKart.SelectedIndex > 0) ustKartId = _ustKartlar[cmbUstKart.SelectedIndex - 1].Id;
        var k = new StokKarti { Id = _mevcut?.Id ?? 0, Ad = txtAd.Text.Trim(), KodNo = txtKodNo.Text.Trim(),
            Aciklama = txtAciklama.Text.Trim(), MinStok = kartTipi == "Alt" ? minStok : 0,
            Kategori = cmbKategori.SelectedItem?.ToString() ?? "", KartTipi = kartTipi, UstKartId = ustKartId };
        if (_mevcut == null) Program.DB!.StokKartiEkle(k); else Program.DB!.StokKartiGuncelle(k);
        DialogResult = DialogResult.OK;
    }
}
