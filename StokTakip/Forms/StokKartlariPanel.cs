using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class StokKartlariPanel : UserControl
{
    private DataGridView grid = new();
    private TextBox txtAra = new();
    private Label lblInfo = new(), lblStatus = new();
    private List<StokKarti> _tumListe = new();

    public StokKartlariPanel()
    {
        BackColor = UIHelper.BgDark; Dock = DockStyle.Fill; DoubleBuffered = true;

        // Header
        var pnlH = UIHelper.MakeHeader(L("stock_cards"));
        lblInfo = new Label { Font = UIHelper.FontSubtitle, ForeColor = UIHelper.TextMuted, Left = 28, Top = 36, AutoSize = true };
        pnlH.Controls.Add(lblInfo);

        // Toolbar — FlowLayoutPanel (otomatik sarma, taşmaz)
        var pnlT = UIHelper.MakeToolbar();
        txtAra = UIHelper.MakeSearchBox(L("search_placeholder")); txtAra.TextChanged += (_, _) => FilterGrid();
        var btnE  = UIHelper.MakeFlowButton(L("new_card"),    UIHelper.AccentBlue, 110);
        var btnD  = UIHelper.MakeFlowButton(L("edit"),        UIHelper.BtnMid, 85);
        var btnS  = UIHelper.MakeFlowButton(L("delete"),      UIHelper.AccentRed, 75);
        var btnTS = UIHelper.MakeFlowButton(L("bulk_delete"), Color.FromArgb(153, 27, 27), 100);
        var btnDet = UIHelper.MakeFlowButton(L("detail"),     UIHelper.AccentGreen, 85);
        var btnTG = UIHelper.MakeFlowButton(L("bulk_entry"),  UIHelper.AccentCyan, 110);
        btnE.Click += (_, _) => { using var f = new StokKartiDuzenleForm(null); if (f.ShowDialog() == DialogResult.OK) YukleGrid(); };
        btnD.Click += (_, _) => { var k = Sec(); if (k != null) { using var f = new StokKartiDuzenleForm(k); if (f.ShowDialog() == DialogResult.OK) YukleGrid(); } };
        btnS.Click += (_, _) => SilKart(); btnTS.Click += (_, _) => TopluSil();
        btnDet.Click += (_, _) => { var k = Sec(); if (k != null) { using var f = new StokKartiDetayForm(k.Id); f.ShowDialog(); YukleGrid(); } };
        btnTG.Click += (_, _) => { using var f = new TopluHareketForm(); if (f.ShowDialog() == DialogResult.OK) YukleGrid(); };
        pnlT.Controls.AddRange(new Control[] { txtAra, btnE, btnD, btnS, btnTS, btnDet, btnTG });

        // Grid
        grid = new DataGridView { Dock = DockStyle.Fill }; UIHelper.StyleGrid(grid, multiSelect: true);
        grid.Columns.Add("Id", "Id"); grid.Columns["Id"]!.Visible = false;
        grid.Columns.Add("KodNo", L("code_no")); grid.Columns["KodNo"]!.FillWeight = 55;
        grid.Columns.Add("Ad", L("stock_name")); grid.Columns["Ad"]!.FillWeight = 150;
        grid.Columns.Add("KartTipi", L("card_type")); grid.Columns["KartTipi"]!.FillWeight = 60;
        grid.Columns.Add("UstKart", L("parent_card_col")); grid.Columns["UstKart"]!.FillWeight = 120;
        grid.Columns.Add("Kategori", L("category")); grid.Columns["Kategori"]!.FillWeight = 85;
        grid.Columns.Add("MevcutStok", L("current_stock")); grid.Columns["MevcutStok"]!.FillWeight = 55;
        grid.Columns.Add("MinStok", L("min_stock")); grid.Columns["MinStok"]!.FillWeight = 45;

        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            string cn = grid.Columns[e.ColumnIndex].Name;
            if (cn == "MevcutStok" && double.TryParse(e.Value?.ToString(), out double s))
            { e.CellStyle.ForeColor = UIHelper.StokRengi(s); e.CellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold); }
            else if (cn == "KartTipi")
            { e.CellStyle.ForeColor = e.Value?.ToString() == L("parent_card") ? UIHelper.AccentCyan : UIHelper.AccentGreen; e.CellStyle.Font = new Font("Segoe UI Semibold", 9f); }
        };
        grid.DoubleClick += (_, _) => { var k = Sec(); if (k != null) { using var f = new StokKartiDetayForm(k.Id); f.ShowDialog(); YukleGrid(); } };
        grid.SelectionChanged += (_, _) => Info();

        // Status
        var pnlSt = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = UIHelper.BgPanel };
        lblStatus = new Label { Left = 20, Top = 5, AutoSize = true, Font = new Font("Segoe UI Semibold", 8.5f), ForeColor = UIHelper.TextMuted };
        pnlSt.Controls.Add(lblStatus);

        Controls.Add(grid); Controls.Add(pnlT); Controls.Add(pnlH); Controls.Add(pnlSt);
        YukleGrid();
    }

    void Info() { int s = grid.SelectedRows.Count; lblInfo.Text = L("records_info", _tumListe.Count, s); lblStatus.Text = s > 1 ? L("rows_selected", s) : ""; }
    void YukleGrid() { _tumListe = Program.DB!.StokKartlariniGetir(); FilterGrid(); }
    void FilterGrid()
    {
        grid.Rows.Clear(); var a = txtAra.Text.ToLowerInvariant();
        foreach (var k in _tumListe)
        {
            if (!string.IsNullOrEmpty(a) && !k.Ad.ToLowerInvariant().Contains(a) && !k.KodNo.ToLowerInvariant().Contains(a) && !k.Aciklama.ToLowerInvariant().Contains(a) && !k.UstKartAd.ToLowerInvariant().Contains(a)) continue;
            grid.Rows.Add(k.Id, k.KodNo, k.Ad, k.KartTipi == "Ust" ? L("parent_card") : L("child_card"), string.IsNullOrEmpty(k.UstKartAd) ? "-" : k.UstKartAd, k.Kategori, UIHelper.FormatMiktar(k.MevcutStok), k.KartTipi == "Alt" ? k.MinStok.ToString() : "");
        }
        Info();
    }
    StokKarti? Sec() { if (grid.SelectedRows.Count == 0) { MessageBox.Show(L("select_row_first")); return null; } return _tumListe.Find(k => k.Id == Convert.ToInt32(grid.SelectedRows[0].Cells["Id"].Value)); }
    void SilKart() { var k = Sec(); if (k == null) return; if (MessageBox.Show(L("confirm_delete", k.Ad), L("confirm_delete_title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) { Program.DB!.StokKartiSil(k.Id); YukleGrid(); } }
    void TopluSil()
    {
        if (grid.SelectedRows.Count < 2) { MessageBox.Show(L("bulk_delete_min")); return; }
        int c = grid.SelectedRows.Count;
        if (MessageBox.Show(L("confirm_bulk_delete", c), L("confirm_bulk_delete_title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        foreach (DataGridViewRow r in grid.SelectedRows) Program.DB!.StokKartiSil(Convert.ToInt32(r.Cells["Id"].Value));
        YukleGrid(); MessageBox.Show(L("bulk_delete_success", c));
    }
}
