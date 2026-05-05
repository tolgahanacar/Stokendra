using System.Drawing.Printing;
using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public sealed class StoklarPanel : UserControl
{
    private static readonly Font StokBoldFont = new("Segoe UI", 10.5f, FontStyle.Bold);
    private DataGridView grid = new();
    private TextBox txtAra = new();
    private Label lblInfo = new();
    private List<StokKarti> _altKartlar = new();
    private int _currentPage = 1;
    private int _pageSize = 50;
    private Button btnPrev = new(), btnNext = new();

    public StoklarPanel()
    {
        BackColor = UIHelper.BgDark; Dock = DockStyle.Fill; DoubleBuffered = true;

        var pnlH = UIHelper.MakeHeader(L("stocks"));

        var pnlT = UIHelper.MakeToolbar();
        txtAra = UIHelper.MakeSearchBox(L("stock_code_search"), 300); txtAra.TextChanged += (_, _) => { _currentPage = 1; FilterGrid(); };
        
        var btnRapor = UIHelper.MakeFlowButton(L("report_al"), UIHelper.AccentBlue, 110);
        btnRapor.Click += (_, _) => RaporAl();
        
        var btnExcel = UIHelper.MakeFlowButton(L("export_excel"), UIHelper.AccentGreen, 130);
        btnExcel.Click += (_, _) => ExcelExport();
        
        pnlT.Controls.AddRange(new Control[] { txtAra, btnRapor, btnExcel });

        grid = new DataGridView { Dock = DockStyle.Fill }; UIHelper.StyleGrid(grid);
        grid.Columns.Add("Id", "Id"); grid.Columns["Id"]!.Visible = false;
        grid.Columns.Add("KodNo", L("code_no")); grid.Columns["KodNo"]!.FillWeight = 55;
        grid.Columns.Add("Ad", L("stock_name")); grid.Columns["Ad"]!.FillWeight = 170;
        grid.Columns.Add("UstKart", L("parent_card_col")); grid.Columns["UstKart"]!.FillWeight = 150;
        grid.Columns.Add("Kategori", L("category")); grid.Columns["Kategori"]!.FillWeight = 100;
        grid.Columns.Add("MevcutStok", L("current_stock")); grid.Columns["MevcutStok"]!.FillWeight = 60;
        grid.Columns.Add("MinStok", L("min_stock")); grid.Columns["MinStok"]!.FillWeight = 50;

        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (grid.Columns[e.ColumnIndex].Name == "MevcutStok" && double.TryParse(e.Value?.ToString(), out double s))
            {
                if (e.CellStyle != null)
                {
                    e.CellStyle.ForeColor = UIHelper.StokRengi(s);
                    e.CellStyle.Font = StokBoldFont;
                }
                
                if (s <= 3)
                    foreach (DataGridViewCell cell in grid.Rows[e.RowIndex].Cells)
                        if (cell.ColumnIndex != e.ColumnIndex && cell.Style != null)
                            cell.Style.ForeColor = s <= 0 ? UIHelper.StokWarning : UIHelper.StokLow;
            }
        };

        grid.DoubleClick += (_, _) =>
        {
            if (grid.SelectedRows.Count == 0) return;
            using var f = new StokKartiDetayForm(Convert.ToInt32(grid.SelectedRows[0].Cells["Id"].Value));
            f.ShowDialog(); YukleGrid();
        };

        // Status
        var pnlSt = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = UIHelper.BgPanel };
        lblInfo = new Label { Left = 20, Top = 12, AutoSize = true, Font = new Font("Segoe UI Semibold", 9f), ForeColor = UIHelper.TextMuted };
        btnPrev = UIHelper.MakeButton("<", UIHelper.BtnMid, 0, 5, 40, 30);
        btnNext = UIHelper.MakeButton(">", UIHelper.BtnMid, 0, 5, 40, 30);
        btnPrev.Click += (_, _) => { if (_currentPage > 1) { _currentPage--; FilterGrid(); } };
        btnNext.Click += (_, _) => { _currentPage++; FilterGrid(); };
        pnlSt.Controls.AddRange(new Control[] { lblInfo, btnPrev, btnNext });
        pnlSt.Resize += (_, _) => { btnNext.Left = pnlSt.Width - 60; btnPrev.Left = pnlSt.Width - 110; };

        Controls.Add(grid); Controls.Add(pnlT); Controls.Add(pnlH); Controls.Add(pnlSt);
        YukleGrid();
    }

    void YukleGrid() { _altKartlar = Program.DB!.AltKartlariGetir(); FilterGrid(); }

    void FilterGrid()
    {
        grid.Rows.Clear(); var a = txtAra.Text.Trim().ToLowerInvariant(); int dusuk = 0;
        var filtered = new List<StokKarti>();
        foreach (var k in _altKartlar)
        {
            if (!string.IsNullOrEmpty(a) && !k.Ad.ToLowerInvariant().Contains(a) && !k.KodNo.ToLowerInvariant().Contains(a) && !k.UstKartAd.ToLowerInvariant().Contains(a)) continue;
            filtered.Add(k);
            if (k.MevcutStok <= 3) dusuk++;
        }
        
        int totalPages = (int)Math.Ceiling(filtered.Count / (double)_pageSize);
        if (totalPages == 0) totalPages = 1;
        if (_currentPage > totalPages) _currentPage = totalPages;
        
        btnPrev.Enabled = _currentPage > 1;
        btnNext.Enabled = _currentPage < totalPages;

        var paged = filtered.Skip((_currentPage - 1) * _pageSize).Take(_pageSize);
        foreach (var k in paged)
        {
            grid.Rows.Add(k.Id, k.KodNo, k.Ad, string.IsNullOrEmpty(k.UstKartAd) ? "-" : k.UstKartAd, k.Kategori, UIHelper.FormatMiktar(k.MevcutStok), k.MinStok);
        }
        lblInfo.Text = L("stocks_subtitle") + $"  •  {filtered.Count} {L("child_card").ToLower()}  •  ⚠ {dusuk} {L("low_stock").ToLower()}  |  Sayfa: {_currentPage} / {totalPages}";
    }

    private void ExcelExport()
    {
        if (_altKartlar.Count == 0) return;
        using var dlg = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = $"Stoklar_{DateTime.Now:yyyyMMdd}.xlsx" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.AddWorksheet(L("stocks"));
        string[] headers = { L("code_no"), L("stock_name"), L("parent_card_col"), L("category"), L("current_stock"), L("min_stock") };
        for (int i = 0; i < headers.Length; i++) { ws.Cell(1, i + 1).Value = headers[i]; ws.Cell(1, i + 1).Style.Font.Bold = true; ws.Cell(1, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(30, 60, 110); ws.Cell(1, i + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White; }
        for (int i = 0; i < _altKartlar.Count; i++)
        {
            var k = _altKartlar[i];
            ws.Cell(i + 2, 1).Value = k.KodNo; ws.Cell(i + 2, 2).Value = k.Ad;
            ws.Cell(i + 2, 3).Value = string.IsNullOrEmpty(k.UstKartAd) ? "-" : k.UstKartAd;
            ws.Cell(i + 2, 4).Value = k.Kategori; ws.Cell(i + 2, 5).Value = k.MevcutStok; ws.Cell(i + 2, 6).Value = k.MinStok;
        }
        ws.Columns().AdjustToContents();
        try { wb.SaveAs(dlg.FileName); MessageBox.Show(L("export_success", dlg.FileName)); }
        catch (Exception ex) { MessageBox.Show(ex.Message, L("error")); }
    }

    private async void RaporAl()
    {
        if (_altKartlar.Count == 0) { MessageBox.Show(L("report_no_data"), L("info")); return; }
        
        string firma = Program.Settings.CompanyName;
        var pd = new PrintDocument();
        pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169); // Portrait
        pd.DefaultPageSettings.Margins = new Margins(40, 40, 50, 50);

        // Optimized: Single query instead of N+1
        var raporVerisi = await Program.DB!.StokRaporVerisiAsync();

        // Use filtered data if search is active
        var filter = txtAra.Text.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(filter))
            raporVerisi = raporVerisi.Where(x => x.KodNo.ToLowerInvariant().Contains(filter) || x.Ad.ToLowerInvariant().Contains(filter)).ToList();

        double grandGiris = raporVerisi.Sum(x => x.ToplamGiris);
        double grandCikis = raporVerisi.Sum(x => x.ToplamCikis);
        double grandMevcut = raporVerisi.Sum(x => x.Mevcut);

        int ps = 0;
        int pageNum = 0;

        pd.BeginPrint += (_, _) => { ps = 0; pageNum = 0; };
        
        pd.PrintPage += (_, e) => {
            var g = e.Graphics!; float y = e.MarginBounds.Top, lm = e.MarginBounds.Left, pw = e.MarginBounds.Width;
            float bottomLimit = e.MarginBounds.Bottom - 30; // Reserve space for footer
            pageNum++;
            
            using var fTitle = new Font("Segoe UI", 16, FontStyle.Bold); 
            using var fSub = new Font("Segoe UI", 9); 
            using var fHeader = new Font("Segoe UI", 9.5f, FontStyle.Bold); 
            using var fRow = new Font("Segoe UI", 9f); 
            using var fTotal = new Font("Segoe UI", 11, FontStyle.Bold); 
            using var fTotalLbl = new Font("Segoe UI", 10, FontStyle.Bold); 

            using var br = new SolidBrush(Color.Black); 
            using var brMuted = new SolidBrush(Color.FromArgb(100, 100, 100)); 
            using var brGreen = new SolidBrush(Color.DarkGreen); 
            using var brRed = new SolidBrush(Color.DarkRed);
            using var brBlue = new SolidBrush(Color.DarkBlue);
            
            using var pen = new Pen(Color.FromArgb(200, 205, 215));
            using var brHd = new SolidBrush(Color.FromArgb(230, 235, 245));
            using var brAlt = new SolidBrush(Color.FromArgb(245, 247, 252));

            // Table column widths
            float[] w = { 90, 270, 110, 110, 110 }; 
            float u = 0; foreach (var ww in w) u += ww; w[w.Length - 1] = pw - (u - w[w.Length - 1]);
            string[] hdr = { L("code_no"), L("stock_name"), L("entry"), L("exit"), L("current_stock") };

            // Draw Report Header (first page only)
            if (ps == 0) { 
                if (!string.IsNullOrWhiteSpace(firma)) { 
                    using var ff = new Font("Segoe UI", 11, FontStyle.Bold); 
                    g.DrawString(firma, ff, br, lm, y); 
                    y += 24; 
                } 
                g.DrawString(L("stocks").ToUpperInvariant(), fTitle, br, lm, y); y += 30; 
                g.DrawString(L("report_date", DateTime.Now.ToString("dd.MM.yyyy HH:mm")), fSub, brMuted, lm, y); y += 20; 
                g.DrawLine(pen, lm, y, lm + pw, y); y += 15; 
            }

            // Draw Table Headers
            void DrawTableHeaders()
            {
                g.FillRectangle(brHd, lm, y, pw, 22);
                float x = lm;
                for (int i = 0; i < hdr.Length; i++) { 
                    using var sf = new StringFormat{ Alignment = i >= 2 ? StringAlignment.Far : StringAlignment.Near };
                    g.DrawString(hdr[i], fHeader, br, new RectangleF(x, y + 3, w[i] - 5, 20), sf); 
                    x += w[i]; 
                } 
                y += 26;
            }

            DrawTableHeaders();

            // Print data rows until we run out of space or data
            while (ps < raporVerisi.Count)
            {
                // Check if there is room for a data row (20px) + possible totals section (90px)
                if (y + 20 > bottomLimit)
                {
                    // No room — break to next page
                    break;
                }

                var item = raporVerisi[ps];
                if (ps % 2 == 0) g.FillRectangle(brAlt, lm, y, pw, 20);
                
                float x = lm;
                g.DrawString(item.KodNo, fRow, br, new RectangleF(x, y + 2, w[0], 20)); x += w[0];
                using var sfEllipsis = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                g.DrawString(item.Ad, fRow, br, new RectangleF(x, y + 2, w[1]-5, 20), sfEllipsis); x += w[1];
                
                using var sfRight = new StringFormat{ Alignment = StringAlignment.Far };
                g.DrawString(UIHelper.FormatMiktar(item.ToplamGiris), fRow, brGreen, new RectangleF(x, y + 2, w[2]-5, 20), sfRight); x += w[2];
                g.DrawString(UIHelper.FormatMiktar(item.ToplamCikis), fRow, brRed, new RectangleF(x, y + 2, w[3]-5, 20), sfRight); x += w[3];
                g.DrawString(UIHelper.FormatMiktar(item.Mevcut), fRow, br, new RectangleF(x, y + 2, w[4]-5, 20), sfRight);
                
                g.DrawLine(pen, lm, y + 20, lm + pw, y + 20); 
                y += 20;
                ps++;
            }

            // Print Grand Totals (only after all rows are printed)
            if (ps >= raporVerisi.Count)
            {
                // Check if there is room for the totals box (90px)
                if (y + 90 > e.MarginBounds.Bottom)
                {
                    // Not enough room — totals go to next page
                    int tp = (int)Math.Ceiling((double)raporVerisi.Count / 40.0);
                    g.DrawString(L("total_records_page", raporVerisi.Count, pageNum, Math.Max(tp, pageNum + 1)), fSub, brMuted, lm, e.MarginBounds.Bottom - 8);
                    e.HasMorePages = true;
                    return;
                }

                y += 15;
                using var brTotBg = new SolidBrush(Color.FromArgb(235, 240, 245));
                using var penTot = new Pen(Color.FromArgb(150, 160, 180));
                g.FillRectangle(brTotBg, lm, y, pw, 60);
                g.DrawRectangle(penTot, lm, y, pw, 60);
                
                y += 8;
                g.DrawString(L("total_consumption").ToUpperInvariant(), fTotalLbl, brMuted, lm + 10, y + 10);
                
                float totalX = lm + w[0] + w[1];
                var sfR = new StringFormat{ Alignment = StringAlignment.Far };
                
                g.DrawString(L("entry") + ":", fSub, brMuted, new RectangleF(totalX, y - 2, w[2]-5, 20), sfR);
                g.DrawString(UIHelper.FormatMiktar(grandGiris), fTotal, brGreen, new RectangleF(totalX, y + 15, w[2]-5, 30), sfR);
                totalX += w[2];

                g.DrawString(L("exit") + ":", fSub, brMuted, new RectangleF(totalX, y - 2, w[3]-5, 20), sfR);
                g.DrawString(UIHelper.FormatMiktar(grandCikis), fTotal, brRed, new RectangleF(totalX, y + 15, w[3]-5, 30), sfR);
                totalX += w[3];
                
                g.DrawString(L("current_stock") + ":", fSub, brMuted, new RectangleF(totalX, y - 2, w[4]-5, 20), sfR);
                g.DrawString(UIHelper.FormatMiktar(grandMevcut), fTotal, brBlue, new RectangleF(totalX, y + 15, w[4]-5, 30), sfR);
            }

            int totalPages = (int)Math.Ceiling((double)raporVerisi.Count / 40.0);
            g.DrawString(L("total_records_page", raporVerisi.Count, pageNum, Math.Max(totalPages, pageNum)), fSub, brMuted, lm, e.MarginBounds.Bottom - 8); 
            e.HasMorePages = ps < raporVerisi.Count;
        };

        using var pv = new PrintPreviewDialog { Document = pd, Width = 900, Height = 1000, StartPosition = FormStartPosition.CenterParent }; 
        pv.ShowDialog();
    }
}
