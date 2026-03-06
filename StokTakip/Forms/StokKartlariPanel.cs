using StokTakip.Models;
using StokTakip.Data;
using ClosedXML.Excel;
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
        var pnlT = UIHelper.MakeToolbar();
        txtAra = UIHelper.MakeSearchBox(L("search_placeholder")); txtAra.TextChanged += (_, _) => FilterGrid();
        var btnE  = UIHelper.MakeFlowButton(L("new_card"),    UIHelper.AccentBlue, 110);
        var btnD  = UIHelper.MakeFlowButton(L("edit"),        UIHelper.BtnMid, 85);
        var btnS  = UIHelper.MakeFlowButton(L("delete"),      UIHelper.AccentRed, 75);
        var btnTS = UIHelper.MakeFlowButton(L("bulk_delete"), Color.FromArgb(153, 27, 27), 100);
        var btnDet = UIHelper.MakeFlowButton(L("detail"),     UIHelper.AccentGreen, 85);
        var btnTG = UIHelper.MakeFlowButton(L("bulk_entry"),  UIHelper.AccentCyan, 110);
        var btnExcel = UIHelper.MakeFlowButton(L("export_excel"), UIHelper.AccentGreen, 130);
        var btnImport = UIHelper.MakeFlowButton(L("import_csv"), UIHelper.AccentPurple, 130);

        btnE.Click += (_, _) => { using var f = new StokKartiDuzenleForm(null); if (f.ShowDialog() == DialogResult.OK) YukleGrid(); };
        btnD.Click += (_, _) => { var k = Sec(); if (k != null) { using var f = new StokKartiDuzenleForm(k); if (f.ShowDialog() == DialogResult.OK) YukleGrid(); } };
        btnS.Click += (_, _) => SilKart(); btnTS.Click += (_, _) => TopluSil();
        btnDet.Click += (_, _) => { var k = Sec(); if (k != null) { using var f = new StokKartiDetayForm(k.Id); f.ShowDialog(); YukleGrid(); } };
        btnTG.Click += (_, _) => { using var f = new TopluHareketForm(); if (f.ShowDialog() == DialogResult.OK) YukleGrid(); };
        btnExcel.Click += (_, _) => ExcelExport();
        btnImport.Click += (_, _) => CsvImport();
        
        pnlT.Controls.AddRange(new Control[] { txtAra, btnE, btnD, btnS, btnTS, btnDet, btnTG, btnExcel, btnImport });

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
            if (e.CellStyle != null)
            {
                if (cn == "MevcutStok" && double.TryParse(e.Value?.ToString(), out double s))
                { e.CellStyle.ForeColor = UIHelper.StokRengi(s); e.CellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold); }
                else if (cn == "KartTipi")
                { e.CellStyle.ForeColor = e.Value?.ToString() == L("parent_card") ? UIHelper.AccentCyan : UIHelper.AccentGreen; e.CellStyle.Font = new Font("Segoe UI Semibold", 9f); }
            }
        };
        grid.DoubleClick += (_, _) => { var k = Sec(); if (k != null) { using var f = new StokKartiDetayForm(k.Id); f.ShowDialog(); YukleGrid(); } };
        grid.SelectionChanged += (_, _) => Info();

        // Status
        var pnlSt = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = UIHelper.BgPanel };
        lblInfo = new Label { Left = 20, Top = 8, AutoSize = true, Font = new Font("Segoe UI Semibold", 9f), ForeColor = UIHelper.TextMuted };
        lblStatus = new Label { Left = 250, Top = 8, AutoSize = true, Font = new Font("Segoe UI Semibold", 9f), ForeColor = UIHelper.AccentBlue };
        pnlSt.Controls.Add(lblInfo); pnlSt.Controls.Add(lblStatus);

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

    private void ExcelExport()
    {
        if (_tumListe.Count == 0) return;
        using var dlg = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = $"StokKartlari_{DateTime.Now:yyyyMMdd}.xlsx" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("StokKartlari");
        ws.Cell(1, 1).Value = "KodNo"; ws.Cell(1, 2).Value = "Ad";
        ws.Cell(1, 3).Value = "KartTipi"; ws.Cell(1, 4).Value = "UstKartAd";
        ws.Cell(1, 5).Value = "Kategori"; ws.Cell(1, 6).Value = "MevcutStok";
        ws.Cell(1, 7).Value = "KritikStok"; ws.Cell(1, 8).Value = "Aciklama";

        var hR = ws.Range("A1:H1");
        hR.Style.Font.Bold = true; hR.Style.Fill.BackgroundColor = XLColor.AirForceBlue; hR.Style.Font.FontColor = XLColor.White;

        for (int i = 0; i < _tumListe.Count; i++)
        {
            var k = _tumListe[i];
            ws.Cell(i + 2, 1).Value = k.KodNo; ws.Cell(i + 2, 2).Value = k.Ad;
            ws.Cell(i + 2, 3).Value = k.KartTipi; ws.Cell(i + 2, 4).Value = k.UstKartAd;
            ws.Cell(i + 2, 5).Value = k.Kategori; ws.Cell(i + 2, 6).Value = k.MevcutStok;
            ws.Cell(i + 2, 7).Value = k.MinStok; ws.Cell(i + 2, 8).Value = k.Aciklama;
        }
        ws.Columns().AdjustToContents();
        try { wb.SaveAs(dlg.FileName); MessageBox.Show(L("export_success", dlg.FileName)); }
        catch (Exception ex) { MessageBox.Show(ex.Message, L("error")); }
    }

    private void CsvImport()
    {
        using var dlg = new OpenFileDialog { Filter = "CSV|*.csv", Title = L("import_csv") };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            var lines = File.ReadAllLines(dlg.FileName);
            if (lines.Length <= 1) return;

            int eklenen = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                var row = lines[i].Split(';');
                if (row.Length < 3) continue;

                var k = new StokKarti
                {
                    KodNo = row[0].Trim(),
                    Ad = row[1].Trim(),
                    KartTipi = "Alt",
                    Kategori = row[2].Trim(),
                    MevcutStok = 0,
                    MinStok = row.Length > 3 && int.TryParse(row[3].Trim(), out int ms) ? ms : 0,
                    Aciklama = "" // Ignore Aciklama for CSV import simplicity or format it if provided
                };
                if (!string.IsNullOrEmpty(k.KodNo) && !string.IsNullOrEmpty(k.Ad))
                {
                    Program.DB!.StokKartiEkle(k);
                    eklenen++;
                }
            }
            YukleGrid();
            MessageBox.Show(L("import_success", eklenen), "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, L("error")); }
    }
}
