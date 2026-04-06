using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

/// <summary>Hareket ekleme VE düzenleme formu</summary>
public sealed class HareketEkleForm : Form
{
    private ComboBox cmbStok = new(), cmbTur = new(), cmbDept = new();
    private TextBox txtMiktar = new(), txtTeslim = new(), txtAciklama = new(), txtKodAra = new();
    private DateTimePicker dtpTarih = new();
    private List<StokKarti> _altKartlar = new();
    private Label lblMevcut = new();
    private readonly StokHareketi? _mevcut;

    public HareketEkleForm(StokKarti? onceden = null) : this(onceden, null) { }

    public HareketEkleForm(StokKarti? onceden, StokHareketi? mevcut)
    {
        _mevcut = mevcut;
        bool edit = mevcut != null;
        Text = edit ? L("edit_movement") : L("new_movement");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        BackColor = UIHelper.BgPanel;

        var strip = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(30, 37, 52) };
        strip.Controls.AddRange(new Control[] {
            new Label { Text = edit ? L("edit_movement") : L("new_movement"), Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = edit ? UIHelper.AccentOrange : UIHelper.AccentGreen, Left = 16, Top = 6, AutoSize = true },
            new Label { Text = L("only_child_cards_can_move"), Font = new Font("Segoe UI", 7.5f), ForeColor = UIHelper.AccentYellow, Left = 16, Top = 28, AutoSize = true }
        });

        var tbl = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16, 8, 16, 8),
            BackColor = UIHelper.BgPanel, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        int row = 0;

        // Stok kodu arama
        tbl.Controls.Add(ML(L("code_no")), 0, row);
        txtKodAra = MakeTB(); txtKodAra.PlaceholderText = L("stock_code_search");
        txtKodAra.TextChanged += (_, _) => KodFiltrele();
        tbl.Controls.Add(txtKodAra, 1, row); row++;

        // Stok Kartı
        tbl.Controls.Add(ML(L("stock_card_required")), 0, row);
        cmbStok = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList }; UIHelper.StyleComboBox(cmbStok);
        tbl.Controls.Add(cmbStok, 1, row); row++;

        // Mevcut
        tbl.Controls.Add(new Label(), 0, row);
        lblMevcut = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8), Height = 18 };
        tbl.Controls.Add(lblMevcut, 1, row); row++;

        // Tür
        tbl.Controls.Add(ML(L("operation_type")), 0, row);
        cmbTur = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 }; UIHelper.StyleComboBox(cmbTur);
        cmbTur.Items.AddRange(new object[] { L("entry"), L("exit"), L("type_empty") }); cmbTur.SelectedIndex = 0;
        tbl.Controls.Add(cmbTur, 1, row); row++;

        // Miktar
        tbl.Controls.Add(ML(L("quantity_required")), 0, row);
        txtMiktar = MakeTB(); txtMiktar.Text = "1"; txtMiktar.Width = 80;
        tbl.Controls.Add(txtMiktar, 1, row); row++;

        // Departman
        tbl.Controls.Add(ML(L("department_required")), 0, row);
        cmbDept = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList }; UIHelper.StyleComboBox(cmbDept);
        foreach (var d in Program.DB!.DepartmanlariGetir()) cmbDept.Items.Add(d);
        if (cmbDept.Items.Count > 0) cmbDept.SelectedIndex = 0;
        tbl.Controls.Add(cmbDept, 1, row); row++;

        // Teslim
        tbl.Controls.Add(ML(L("delivered_to_label")), 0, row);
        txtTeslim = MakeTB(); tbl.Controls.Add(txtTeslim, 1, row); row++;

        // Tarih
        tbl.Controls.Add(ML(L("date_label")), 0, row);
        dtpTarih = new DateTimePicker { Dock = DockStyle.Fill, Value = DateTime.Now }; UIHelper.StyleDatePicker(dtpTarih, true);
        tbl.Controls.Add(dtpTarih, 1, row); row++;

        // Açıklama
        tbl.Controls.Add(ML(L("description_label")), 0, row);
        txtAciklama = MakeTB(); txtAciklama.MaxLength = 500;
        tbl.Controls.Add(txtAciklama, 1, row); row++;


        // Butonlar
        var pnlBtn = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Height = 40, Padding = new Padding(0, 4, 0, 0) };
        var btnI = UIHelper.MakeButton(L("cancel"), UIHelper.BtnBarBg, 0, 0, 88, 32); btnI.Dock = DockStyle.None;
        var btnK = UIHelper.MakeButton(L("save"), edit ? UIHelper.AccentOrange : UIHelper.AccentGreen, 0, 0, 100, 32); btnK.Dock = DockStyle.None;
        btnK.Click += Kaydet; btnI.Click += (_, _) => DialogResult = DialogResult.Cancel;
        pnlBtn.Controls.AddRange(new Control[] { btnI, btnK });
        tbl.Controls.Add(new Label(), 0, row);
        tbl.Controls.Add(pnlBtn, 1, row);

        // Alt kartları yükle
        _altKartlar = Program.DB!.AltKartlariGetir();
        foreach (var k in _altKartlar) cmbStok.Items.Add(k);

        // Mevcut düzenleme verileri
        if (edit && mevcut != null)
        {
            int idx = _altKartlar.FindIndex(k => k.Id == mevcut.StokKartId); if (idx >= 0) cmbStok.SelectedIndex = idx;
            cmbTur.SelectedIndex = mevcut.Tur == nameof(HareketTuru.Giris) ? 0 : (mevcut.Tur == nameof(HareketTuru.Cikis) ? 1 : 2);
            txtMiktar.Text = UIHelper.FormatMiktar(mevcut.Miktar);
            txtTeslim.Text = mevcut.TeslimEdilen;
            txtAciklama.Text = mevcut.Aciklama;
            dtpTarih.Value = mevcut.Tarih;
            for (int i = 0; i < cmbDept.Items.Count; i++) if (cmbDept.Items[i]?.ToString() == mevcut.Departman) { cmbDept.SelectedIndex = i; break; }
        }
        else if (onceden != null && onceden.KartTipi == nameof(KartTipi.Alt))
        { int idx = _altKartlar.FindIndex(k => k.Id == onceden.Id); if (idx >= 0) cmbStok.SelectedIndex = idx; }
        else if (_altKartlar.Count > 0) cmbStok.SelectedIndex = 0;

        cmbStok.SelectedIndexChanged += (_, _) => StokBilgi();
        StokBilgi();

        Controls.Add(tbl); Controls.Add(strip);
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; MinimumSize = new Size(470, 100);
    }

    static Label ML(string t) => new Label { Text = t, Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 9), ForeColor = UIHelper.TextSecondary, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 2, 6, 2) };
    static TextBox MakeTB() { var t = new TextBox { Dock = DockStyle.Fill }; UIHelper.StyleTextBox(t); return t; }

    private void KodFiltrele()
    {
        var a = txtKodAra.Text.Trim().ToLowerInvariant(); cmbStok.Items.Clear();
        var f = string.IsNullOrEmpty(a) ? _altKartlar : _altKartlar.Where(k => k.KodNo.ToLowerInvariant().Contains(a) || k.Ad.ToLowerInvariant().Contains(a)).ToList();
        foreach (var k in f) cmbStok.Items.Add(k);
        if (f.Count > 0) cmbStok.SelectedIndex = 0; else { cmbStok.SelectedIndex = -1; lblMevcut.Text = ""; }
    }

    private void StokBilgi()
    {
        if (cmbStok.SelectedItem is not StokKarti k) { lblMevcut.Text = ""; return; }
        lblMevcut.ForeColor = UIHelper.StokRengi(k.MevcutStok);
        lblMevcut.Text = L("current_label", UIHelper.FormatMiktar(k.MevcutStok));
    }

    private void Kaydet(object? s, EventArgs e)
    {
        if (cmbStok.SelectedItem is not StokKarti sk) { MessageBox.Show(L("select_stock_card")); return; }
        
        bool isBos = cmbTur.SelectedIndex == 2;
        if (!double.TryParse(txtMiktar.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double m) || (!isBos && m <= 0))
        {
            if (isBos) m = 0; // Allow parsing to fail gracefully to 0 for Empty type if txtMiktar is completely blank
            else { MessageBox.Show(L("enter_valid_qty")); return; }
        }
        if (cmbDept.SelectedIndex < 0) { MessageBox.Show(L("select_dept")); return; }

        var h = new StokHareketi
        {
            Id = _mevcut?.Id ?? 0,
            StokKartId = sk.Id, Tur = cmbTur.SelectedIndex == 0 ? nameof(HareketTuru.Giris) : (cmbTur.SelectedIndex == 1 ? nameof(HareketTuru.Cikis) : nameof(HareketTuru.Bos)),
            Miktar = isBos ? 0 : m, TeslimEdilen = txtTeslim.Text.Trim(),
            Departman = cmbDept.SelectedItem!.ToString()!, Tarih = dtpTarih.Value, Aciklama = txtAciklama.Text.Trim()
        };

        try
        {
            if (_mevcut != null) Program.DB!.HareketGuncelle(h);
            else Program.DB!.HareketEkle(h);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, L("error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
