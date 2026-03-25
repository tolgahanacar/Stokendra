using System.Drawing.Printing;
using System.Text;
using System.Globalization;
using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class StokHareketPanel : UserControl
{
    private DataGridView grid = new();
    private ComboBox cmbStok = new(), cmbDept = new(), cmbTur = new();
    private DateTimePicker dtpBas = new(), dtpBit = new();
    private Label lblInfo = new();
    private TextBox txtSearch = new();
    private List<StokHareketi> _liste = new();

    public StokHareketPanel()
    {
        BackColor = UIHelper.BgDark; Dock = DockStyle.Fill; DoubleBuffered = true;

        var pnlH = UIHelper.MakeHeader(L("stock_movements"));

        // ═══ FILTER BAR (Standart) ═══
        dtpBas = new DateTimePicker { Value = DateTime.Now.AddMonths(-1) };
        dtpBit = new DateTimePicker { Value = DateTime.Now };

        // Stok
        cmbStok = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        UIHelper.StyleComboBox(cmbStok);
        cmbStok.Items.Add(L("all"));
        foreach (var k in Program.DB!.AltKartlariGetir()) cmbStok.Items.Add(k);
        cmbStok.SelectedIndex = 0;

        // Departman
        cmbDept = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        UIHelper.StyleComboBox(cmbDept);
        cmbDept.Items.Add(L("all"));
        foreach (var d in Program.DB!.DepartmanlariGetir()) cmbDept.Items.Add(d);
        cmbDept.SelectedIndex = 0;

        // Tür
        cmbTur = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        UIHelper.StyleComboBox(cmbTur);
        cmbTur.Items.AddRange(new object[] { L("all"), L("entry"), L("exit"), L("type_empty") });
        cmbTur.SelectedIndex = 0;

        // Arama
        txtSearch = UIHelper.MakeSearchBox(L("search_placeholder"), 200);
        txtSearch.TextChanged += (_, _) => ApplyLiveSearch();

        // Butonlar
        var btnFil = UIHelper.MakeFlowButton(L("filter"), UIHelper.AccentBlue, 80, 30);
        var btnClr = UIHelper.MakeFlowButton(L("clear_filter"), UIHelper.BtnDark, 80, 30);
        btnFil.Click += (_, _) => Filtrele();
        btnClr.Click += (_, _) => Temizle();

        // Standart filtre barı
        var pnlF = UIHelper.MakeFilterBar();
        var flow = UIHelper.GetFilterFlow(pnlF);

        var cellDate   = UIHelper.MakeDateRangeCell(L("date_filter"), dtpBas, dtpBit);
        var cellStok   = UIHelper.MakeFilterCell(L("stock_filter"), cmbStok, 120);
        var cellDept   = UIHelper.MakeFilterCell(L("dept_filter"),  cmbDept, 110);
        var cellTur    = UIHelper.MakeFilterCell(L("type_filter"),  cmbTur,  90);
        var cellArama  = UIHelper.MakeFilterCell(L("arama_label"),  txtSearch, 200);
        var cellBtns   = UIHelper.MakeFilterButtons(btnFil, btnClr);

        flow.Controls.AddRange(new Control[] { cellDate, cellStok, cellDept, cellTur, cellArama, cellBtns });

        // Arama kutusunu kalan boşluğa genişlet
        flow.Resize += (_, _) =>
        {
            int used = cellDate.Width + cellStok.Width + cellDept.Width + cellTur.Width + cellBtns.Width
                       + 10 * 6 + flow.Padding.Horizontal + 24;
            int remaining = flow.Width - used;
            if (remaining > 80) { txtSearch.Width = remaining; cellArama.Width = remaining; }
        };


        // Toolbar
        var pnlT = UIHelper.MakeToolbar(46);
        pnlT.AutoSize = true;
        pnlT.WrapContents = true;
        var btnEkle  = UIHelper.MakeFlowButton(L("add_movement"), UIHelper.AccentGreen, 130);
        var btnDuz   = UIHelper.MakeFlowButton(L("edit"), UIHelper.AccentOrange, 85);
        var btnTopluDuz = UIHelper.MakeFlowButton(L("bulk_edit"), UIHelper.AccentOrange, 120);
        var btnSil   = UIHelper.MakeFlowButton(L("delete"), UIHelper.AccentRed, 70);
        var btnTSil  = UIHelper.MakeFlowButton(L("bulk_delete"), Color.FromArgb(153, 27, 27), 100);
        var btnYaz   = UIHelper.MakeFlowButton(L("print"), UIHelper.BtnMid, 90);
        var btnExcel = UIHelper.MakeFlowButton(L("export_excel"), UIHelper.AccentCyan, 130);
        var btnImport = UIHelper.MakeFlowButton(L("import_csv"), UIHelper.AccentPurple, 120);
        btnEkle.Click += (_, _) => { using var f = new HareketEkleForm(); if (f.ShowDialog() == DialogResult.OK) Filtrele(); };
        btnDuz.Click += (_, _) => DuzenleHareket();
        btnTopluDuz.Click += (_, _) => TopluDuzenle();
        btnSil.Click += (_, _) => Sil(); btnTSil.Click += (_, _) => TopluSil();
        btnYaz.Click += (_, _) => Yazdir(); btnExcel.Click += (_, _) => ExcelExport();
        btnImport.Click += (_, _) => XlsxImport();
        var btnOrnek = UIHelper.MakeFlowButton("📄 Ornek XLSX", UIHelper.BtnDark, 110);
        btnOrnek.Click += (_, _) => OrnekDosya();
        pnlT.Controls.AddRange(new Control[] { btnEkle, btnDuz, btnTopluDuz, btnSil, btnTSil, btnYaz, btnExcel, btnImport, btnOrnek });

        // Grid
        grid = new DataGridView { Dock = DockStyle.Fill }; UIHelper.StyleGrid(grid, multiSelect: true);
        grid.Columns.Add("Id", "Id"); grid.Columns["Id"]!.Visible = false;
        grid.Columns.Add("StokKartId", "StokKartId"); grid.Columns["StokKartId"]!.Visible = false;
        grid.Columns.Add("KodNo", L("code_no")); grid.Columns["KodNo"]!.FillWeight = 55;
        grid.Columns.Add("StokAd", L("stock_name")); grid.Columns["StokAd"]!.FillWeight = 140;
        grid.Columns.Add("TeslimEdilen", L("delivered_to")); grid.Columns["TeslimEdilen"]!.FillWeight = 110;
        grid.Columns.Add("GirisCikis", L("operation_type")); grid.Columns["GirisCikis"]!.FillWeight = 65;
        grid.Columns.Add("Dept", L("department")); grid.Columns["Dept"]!.FillWeight = 85;
        grid.Columns.Add("Tarih", L("date")); grid.Columns["Tarih"]!.FillWeight = 75;
        grid.Columns["Tarih"]!.DefaultCellStyle.Format = "dd.MM.yyyy HH:mm";
        grid.Columns.Add("Aciklama", L("description")); grid.Columns["Aciklama"]!.FillWeight = 140;
        // hidden raw columns for edit
        grid.Columns.Add("RawTur", ""); grid.Columns["RawTur"]!.Visible = false;
        grid.Columns.Add("RawMiktar", ""); grid.Columns["RawMiktar"]!.Visible = false;
        grid.Columns.Add("RawTarih", ""); grid.Columns["RawTarih"]!.Visible = false;

        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            if (grid.Columns[e.ColumnIndex].Name == "GirisCikis")
            {
                string v = e.Value?.ToString() ?? "";
                if (e.CellStyle != null)
                {
                    if (v.Contains("[Ç]")) e.CellStyle.ForeColor = UIHelper.StokWarning;
                    else if (v.Contains("[B]")) e.CellStyle.ForeColor = UIHelper.TextSecondary;
                    else e.CellStyle.ForeColor = UIHelper.AccentGreen;
                    e.CellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                }
            }
        };
        grid.DoubleClick += (_, _) => DuzenleHareket();

        // Status
        var pnlSt = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = UIHelper.BgPanel };
        lblInfo = new Label { Left = 20, Top = 8, AutoSize = true, Font = new Font("Segoe UI Semibold", 9f), ForeColor = UIHelper.TextMuted };
        pnlSt.Controls.Add(lblInfo);

        Controls.Add(grid); Controls.Add(pnlT); Controls.Add(pnlF); Controls.Add(pnlH); Controls.Add(pnlSt);
        Filtrele();
    }


    void Filtrele()
    {
        int? kartId = null; string? dept = null, tur = null;
        if (cmbStok.SelectedIndex > 0 && cmbStok.SelectedItem is StokKarti sk) kartId = sk.Id;
        if (cmbDept.SelectedIndex > 0) dept = cmbDept.SelectedItem!.ToString();
        if (cmbTur.SelectedIndex == 1) tur = "Giris"; else if (cmbTur.SelectedIndex == 2) tur = "Cikis"; else if (cmbTur.SelectedIndex == 3) tur = "Bos";
        _liste = Program.DB!.HareketleriGetir(kartId, dtpBas.Value.Date, dtpBit.Value.Date.AddDays(1), dept, tur);
        ApplyLiveSearch();
    }
    
    void ApplyLiveSearch()
    {
        string term = txtSearch.Text.Trim().ToLowerInvariant();
        var data = _liste;
        
        if (!string.IsNullOrEmpty(term))
        {
            data = data.Where(h =>
                (h.StokKartKodNo != null && h.StokKartKodNo.ToLowerInvariant().Contains(term)) ||
                (h.StokKartAd != null && h.StokKartAd.ToLowerInvariant().Contains(term)) ||
                (h.TeslimEdilen != null && h.TeslimEdilen.ToLowerInvariant().Contains(term)) ||
                (h.Departman != null && h.Departman.ToLowerInvariant().Contains(term)) ||
                (h.Aciklama != null && h.Aciklama.ToLowerInvariant().Contains(term))
            ).ToList();
        }

        grid.Rows.Clear();
        foreach (var h in data)
        {
            string gc = h.Tur == "Giris" ? $"{UIHelper.FormatMiktar(h.Miktar)}[G]" : (h.Tur == "Cikis" ? $"{UIHelper.FormatMiktar(h.Miktar)}[Ç]" : $"{UIHelper.FormatMiktar(h.Miktar)}[B]");
            grid.Rows.Add(h.Id, h.StokKartId, h.StokKartKodNo, h.StokKartAd, h.TeslimEdilen, gc, h.Departman, h.Tarih, h.Aciklama,
                h.Tur, h.Miktar.ToString(CultureInfo.InvariantCulture), h.Tarih.ToString("o"));
        }
        lblInfo.Text = L("movements_count", data.Count);
    }

    void Temizle() { dtpBas.Value = DateTime.Now.AddMonths(-1); dtpBit.Value = DateTime.Now; cmbStok.SelectedIndex = 0; cmbDept.SelectedIndex = 0; cmbTur.SelectedIndex = 0; txtSearch.Clear(); Filtrele(); }
    void Sil()
    {
        if (grid.SelectedRows.Count == 0) { MessageBox.Show(L("select_rows_to_delete")); return; }
        var cell = grid.SelectedRows[0].Cells["Id"];
        if (cell?.Value == null) { MessageBox.Show(L("select_rows_to_delete")); return; }
        if (MessageBox.Show(L("confirm_movement_delete"), L("confirm_delete_title"), MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            Program.DB!.HareketSil(Convert.ToInt32(cell.Value)); Filtrele();
        }
    }
    void TopluSil()
    {
        if (grid.SelectedRows.Count < 2) { MessageBox.Show(L("select_rows_to_delete")); return; }
        int c = grid.SelectedRows.Count;
        if (MessageBox.Show(L("confirm_bulk_movement_delete", c), L("confirm_delete_title"), MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        foreach (DataGridViewRow r in grid.SelectedRows)
        {
            var cell = r.Cells["Id"];
            if (cell?.Value == null) continue;
            Program.DB!.HareketSil(Convert.ToInt32(cell.Value));
        }
        Filtrele(); MessageBox.Show(L("bulk_movement_delete_success", c));
    }

    // ═══ DÜZENLE ═══
    void DuzenleHareket()
    {
        if (grid.SelectedRows.Count == 0) { MessageBox.Show(L("select_row_first")); return; }
        var row = grid.SelectedRows[0];
        var h = new StokHareketi
        {
            Id = Convert.ToInt32(row.Cells["Id"].Value),
            StokKartId = Convert.ToInt32(row.Cells["StokKartId"].Value),
            Tur = row.Cells["RawTur"].Value?.ToString() ?? "Giris",
            Miktar = double.TryParse(row.Cells["RawMiktar"].Value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double m) ? m : 0,
            TeslimEdilen = row.Cells["TeslimEdilen"].Value?.ToString() ?? "",
            Departman = row.Cells["Dept"].Value?.ToString() ?? "",
            Aciklama = row.Cells["Aciklama"].Value?.ToString() ?? "",
            Tarih = DateTime.TryParse(row.Cells["RawTarih"].Value?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.Now
        };
        using var f = new HareketEkleForm(null, h);
        if (f.ShowDialog() == DialogResult.OK) Filtrele();
    }

    // ═══ TOPLU DÜZENLE ═══
    void TopluDuzenle()
    {
        if (grid.SelectedRows.Count < 2)
        {
            MessageBox.Show(L("bulk_delete_min")); // Reusing localization for multiple selection requirement
            return;
        }

        var seciliHareketler = new List<StokHareketi>();
        foreach (DataGridViewRow row in grid.SelectedRows)
        {
            seciliHareketler.Add(new StokHareketi
            {
                Id = Convert.ToInt32(row.Cells["Id"].Value),
                StokKartId = Convert.ToInt32(row.Cells["StokKartId"].Value),
                Tur = row.Cells["RawTur"].Value?.ToString() ?? "Giris",
                Miktar = double.TryParse(row.Cells["RawMiktar"].Value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double m) ? m : 0,
                TeslimEdilen = row.Cells["TeslimEdilen"].Value?.ToString() ?? "",
                Departman = row.Cells["Dept"].Value?.ToString() ?? "",
                Aciklama = row.Cells["Aciklama"].Value?.ToString() ?? "",
                Tarih = DateTime.TryParse(row.Cells["RawTarih"].Value?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.Now
            });
        }

        using var tf = new TopluHareketDuzenleForm(seciliHareketler);
        if (tf.ShowDialog() == DialogResult.OK) Filtrele();
    }

    // ═══ XLSX IMPORT (ClosedXML) ═══
    void XlsxImport()
    {
        using var dlg = new OpenFileDialog { Title = L("import_csv"), Filter = "Excel (*.xlsx)|*.xlsx|Eski Excel (*.xls)|*.xls" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            using var wb = new ClosedXML.Excel.XLWorkbook(dlg.FileName);
            var ws = wb.Worksheets.First();
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow < 2) { MessageBox.Show(L("import_no_data")); return; }

            // Header validation — check first 7 columns
            string[] expected = { L("code_no"), L("stock_name"), L("delivered_to"), L("operation_type"), L("department"), L("date"), L("description") };
            var fileHeaders = new List<string>();
            for (int c = 1; c <= Math.Min(7, ws.LastColumnUsed()?.ColumnNumber() ?? 0); c++)
                fileHeaders.Add(ws.Cell(1, c).GetString().Trim());

            bool match = fileHeaders.Count >= 6;
            for (int i = 0; i < Math.Min(expected.Length, fileHeaders.Count) && match; i++)
                if (!expected[i].Equals(fileHeaders[i], StringComparison.OrdinalIgnoreCase)) match = false;

            if (!match)
            {
                MessageBox.Show(L("import_header_mismatch", string.Join(" | ", expected), string.Join(" | ", fileHeaders)), L("error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var altKartlar = Program.DB!.AltKartlariGetir();
            int imported = 0, skipped = 0;
            var warnings = new List<string>();

            for (int r = 2; r <= lastRow; r++)
            {
                string kodNo = ws.Cell(r, 1).GetString().Trim();
                if (string.IsNullOrEmpty(kodNo)) continue;

                var kart = altKartlar.Find(k => k.KodNo.Equals(kodNo, StringComparison.OrdinalIgnoreCase));
                if (kart == null) { warnings.Add(L("import_stock_not_found", r, kodNo)); skipped++; continue; }

                string gcStr = ws.Cell(r, 4).GetString().Trim();
                string tur = gcStr.Contains("[\u00c7]") ? "Cikis" : (gcStr.Contains("[B]") ? "Bos" : "Giris");
                string mStr = gcStr.Replace("[G]", "").Replace("[\u00c7]", "").Replace("[B]", "").Trim();
                if (!double.TryParse(mStr.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double miktar) || (tur != "Bos" && miktar <= 0))
                { if (!double.TryParse(mStr, out miktar) || (tur != "Bos" && miktar <= 0)) { if (tur == "Bos") miktar = 0; else { skipped++; continue; } } }

                string teslim = ws.Cell(r, 3).GetString().Trim();
                string dept = ws.Cell(r, 5).GetString().Trim();
                string aciklama = ws.Cell(r, 7).GetString().Trim();

                DateTime tarih = DateTime.Now;
                string tarihStr = ws.Cell(r, 6).GetString().Trim();
                string[] fmt = { "dd.MM.yyyy HH:mm", "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd" };
                if (!DateTime.TryParseExact(tarihStr, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out tarih))
                    if (ws.Cell(r, 6).TryGetValue(out DateTime dtVal)) tarih = dtVal;

                Program.DB!.HareketEkle(new StokHareketi
                {
                    StokKartId = kart.Id, Tur = tur, Miktar = miktar,
                    TeslimEdilen = teslim, Departman = dept, Tarih = tarih, Aciklama = aciklama
                });
                imported++;
            }

            Filtrele();
            string msg = skipped > 0 ? L("import_skipped", imported, skipped) + (warnings.Count > 0 ? "\n\n" + string.Join("\n", warnings.Take(10)) : "")
                                      : L("import_success", imported);
            MessageBox.Show(msg, L("info"), MessageBoxButtons.OK, skipped > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(L("import_error", ex.Message), L("error"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    // ═══ ÖRNEK DOSYA (XLSX) ═══
    void OrnekDosya()
    {
        try
        {
            string dest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ornek_import.xlsx");
            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.AddWorksheet("Stok Hareketleri");

            string[] headers = { L("code_no"), L("stock_name"), L("delivered_to"), L("operation_type"), L("department"), L("date"), L("description") };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(30, 60, 110);
                ws.Cell(1, i + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            }

            // Sample data
            ws.Cell(2, 1).Value = "001"; ws.Cell(2, 2).Value = "\u00d6rnek \u00dcr\u00fcn"; ws.Cell(2, 3).Value = "Ahmet Y\u0131lmaz";
            ws.Cell(2, 4).Value = "2[G]"; ws.Cell(2, 5).Value = "Bilgi \u0130\u015flem"; ws.Cell(2, 6).Value = "04.03.2026 10:00"; ws.Cell(2, 7).Value = "Giri\u015f \u00f6rne\u011fi";
            ws.Cell(3, 1).Value = "002"; ws.Cell(3, 2).Value = "\u00d6rnek Toner"; ws.Cell(3, 3).Value = "Mehmet Demir";
            ws.Cell(3, 4).Value = "1[\u00c7]"; ws.Cell(3, 5).Value = "Muhasebe"; ws.Cell(3, 6).Value = "04.03.2026 11:00"; ws.Cell(3, 7).Value = "\u00c7\u0131k\u0131\u015f \u00f6rne\u011fi";

            ws.Columns().AdjustToContents();
            wb.SaveAs(dest);
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{dest}\"");
            MessageBox.Show($"\u00d6rnek dosya masa\u00fcst\u00fcne olu\u015fturuldu:\n{dest}", L("info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show("Hata: " + ex.Message); }
    }

    // ═══ XLSX EXPORT (ClosedXML) ═══
    void ExcelExport()
    {
        using var dlg = new SaveFileDialog { Title = L("export_excel"), Filter = "Excel (*.xlsx)|*.xlsx", FileName = $"StokHareketleri_{DateTime.Now:yyyyMMdd_HHmm}.xlsx" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.AddWorksheet("Stok Hareketleri");

            string[] headers = { L("code_no"), L("stock_name"), L("delivered_to"), L("operation_type"), L("department"), L("date"), L("description") };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(30, 60, 110);
                ws.Cell(1, i + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            }

            int row = 2;
            foreach (var h in _liste)
            {
                string gc = h.Tur == "Giris" ? $"{UIHelper.FormatMiktar(h.Miktar)}[G]" : (h.Tur == "Cikis" ? $"{UIHelper.FormatMiktar(h.Miktar)}[\u00c7]" : $"{UIHelper.FormatMiktar(h.Miktar)}[B]");
                ws.Cell(row, 1).Value = h.StokKartKodNo; ws.Cell(row, 2).Value = h.StokKartAd;
                ws.Cell(row, 3).Value = h.TeslimEdilen; ws.Cell(row, 4).Value = gc;
                ws.Cell(row, 5).Value = h.Departman; ws.Cell(row, 6).Value = h.Tarih.ToString("dd.MM.yyyy HH:mm");
                ws.Cell(row, 7).Value = h.Aciklama;
                // Color entry/exit/empty
                ws.Cell(row, 4).Style.Font.FontColor = h.Tur == "Giris" ? ClosedXML.Excel.XLColor.DarkGreen : (h.Tur == "Cikis" ? ClosedXML.Excel.XLColor.DarkRed : ClosedXML.Excel.XLColor.Gray);
                ws.Cell(row, 4).Style.Font.Bold = true;
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(dlg.FileName);
            MessageBox.Show(L("export_success", dlg.FileName), L("info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(L("export_error") + "\n" + ex.Message, L("error"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    // ═══ PRINT — tarihe göre sıralı ═══
    void Yazdir()
    {
        // Tarihe göre sırala (ASC)
        var sorted = _liste.OrderBy(h => h.Tarih).ThenBy(h => h.Id).ToList();

        string firma = Program.Settings.CompanyName; var pd = new PrintDocument();
        pd.DefaultPageSettings.Landscape = true; pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 1169, 827); pd.DefaultPageSettings.Margins = new Margins(40, 40, 50, 50);

        using (var psd = new PageSetupDialog { Document = pd })
        {
            if (psd.ShowDialog() != DialogResult.OK) return;
        }

        int ps = 0; const int rpp = 28; int tp = Math.Max(1, (int)Math.Ceiling((double)sorted.Count / rpp));
        pd.BeginPrint += (_, _) => { ps = 0; };
        pd.PrintPage += (_, e) => {
            var g = e.Graphics!; float y = e.MarginBounds.Top, lm = e.MarginBounds.Left, pw = e.MarginBounds.Width;
            using var fT = new Font("Segoe UI", 13, FontStyle.Bold); using var fS = new Font("Segoe UI", 8); using var fH = new Font("Segoe UI", 7.5f, FontStyle.Bold); using var fC = new Font("Segoe UI", 7.5f);
            using var br = new SolidBrush(Color.Black); using var brG = new SolidBrush(Color.Gray); using var pen = new Pen(Color.FromArgb(180, 185, 200));
            if (ps == 0) { if (!string.IsNullOrWhiteSpace(firma)) { using var ff = new Font("Segoe UI", 9, FontStyle.Bold); g.DrawString(firma, ff, br, lm, y); y += 18; } g.DrawString(L("movements_report"), fT, br, lm, y); y += 24; g.DrawString(L("report_date", DateTime.Now.ToString("dd.MM.yyyy HH:mm")), fS, brG, lm, y); y += 16; g.DrawLine(pen, lm, y, lm + pw, y); y += 6; }
            float[] weights = { 70, 170, 150, 70, 120, 100, 150 };
            float totalWeight = weights.Sum();
            float[] w = weights.Select(wt => (wt / totalWeight) * pw).ToArray();
            string[] hdr = { L("code_no"), L("stock_name"), L("delivered_to"), L("operation_type"), L("department"), L("date"), L("description") };
            using var brHd = new SolidBrush(Color.FromArgb(230, 235, 245)); g.FillRectangle(brHd, lm, y, pw, 16); float x = lm;
            for (int i = 0; i < hdr.Length; i++) { g.DrawString(hdr[i], fH, br, x + 2, y + 2); x += w[i]; } y += 18;
            int end = Math.Min(ps + rpp, sorted.Count); using var brAlt = new SolidBrush(Color.FromArgb(245, 247, 252));
            for (int i = ps; i < end; i++) {
                var h = sorted[i]; if (i % 2 == 0) g.FillRectangle(brAlt, lm, y, pw, 15); x = lm; bool giris = h.Tur == "Giris"; bool bos = h.Tur == "Bos";
                string gc = giris ? $"{UIHelper.FormatMiktar(h.Miktar)}[G]" : (bos ? $"{UIHelper.FormatMiktar(h.Miktar)}[B]" : $"{UIHelper.FormatMiktar(h.Miktar)}[Ç]");
                string[] cells = { h.StokKartKodNo, h.StokKartAd, h.TeslimEdilen, gc, h.Departman, h.Tarih.ToString("dd.MM.yyyy HH:mm"), h.Aciklama };
                for (int c = 0; c < cells.Length; c++) { Color clr = c == 3 ? (giris ? Color.DarkGreen : (bos ? Color.Gray : Color.DarkRed)) : Color.Black; using var brC = new SolidBrush(clr); g.DrawString(cells[c], c == 3 ? fH : fC, brC, new RectangleF(x + 2, y + 1, w[c] - 4, 14), new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }); x += w[c]; }
                g.DrawLine(pen, lm, y + 15, lm + pw, y + 15); y += 16;
            }
            g.DrawString(L("total_records_page", sorted.Count, ps / rpp + 1, tp), fS, brG, lm, e.MarginBounds.Bottom - 8); ps += rpp; e.HasMorePages = ps < sorted.Count;
        };
        using var pv = new PrintPreviewDialog { Document = pd, Width = 1100, Height = 700, StartPosition = FormStartPosition.CenterParent }; pv.ShowDialog(this);
    }
}
