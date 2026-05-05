using System.Drawing.Printing;
using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public sealed class ServislerPanel : UserControl
{
    private DataGridView dgv = new DataGridView();
    private List<ServisKaydi> _kayitlar = new();
    
    // Filter controls
    private DateTimePicker dtpBas = new(), dtpBit = new();
    private TextBox txtArama = new();
    private Label lblInfo = new();

    public ServislerPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = UIHelper.BgDark;
        DoubleBuffered = true;
        BuildUI();
        Filtrele();
    }

    private void BuildUI()
    {
        var pnlH = UIHelper.MakeHeader(L("services", "Servis / Bakım İşlemleri"));

        // ═══ FILTER BAR (Standart) ═══
        dtpBas = new DateTimePicker { Value = new DateTime(2024, 1, 1) };
        dtpBit = new DateTimePicker();
        txtArama = UIHelper.MakeSearchBox(L("service_search_placeholder", "Cihaz adı, seri no..."), 200);
        txtArama.TextChanged += (_, _) => Filtrele();

        var btnFil = UIHelper.MakeFlowButton(L("filter"), UIHelper.AccentBlue, 80, 30);
        var btnClr = UIHelper.MakeFlowButton(L("clear_filter"), UIHelper.BtnDark, 80, 30);
        btnFil.Click += (_, _) => Filtrele(); 
        btnClr.Click += (_, _) => Temizle();

        var pnlF = UIHelper.MakeFilterBar();
        var flow = UIHelper.GetFilterFlow(pnlF);

        var cellDate  = UIHelper.MakeDateRangeCell(L("date_filter"), dtpBas, dtpBit);
        var cellArama = UIHelper.MakeFilterCell(L("arama_label", "Arama:"), txtArama, 200);
        var cellBtns  = UIHelper.MakeFilterButtons(btnFil, btnClr);

        flow.Controls.AddRange(new Control[] { cellDate, cellArama, cellBtns });

        // Arama kutusunu kalan boşluğa genişlet
        flow.Resize += (_, _) =>
        {
            int used = cellDate.Width + cellBtns.Width + 10 * 3 + flow.Padding.Horizontal + 24;
            int remaining = flow.Width - used;
            if (remaining > 80) { txtArama.Width = remaining; cellArama.Width = remaining; }
        };

        // Action Toolbar
        var toolbar = UIHelper.MakeToolbar();
        var btnYeni = UIHelper.MakeFlowButton(L("new_service_record"), UIHelper.AccentBlue, 140);
        var btnDuzenle = UIHelper.MakeFlowButton(L("edit"), UIHelper.AccentOrange, 100);
        var btnSil = UIHelper.MakeFlowButton(L("delete"), UIHelper.AccentRed, 90);
        var btnYaz = UIHelper.MakeFlowButton(L("print"), UIHelper.BtnMid, 90);
        var btnExcel = UIHelper.MakeFlowButton(L("export_excel"), UIHelper.AccentCyan, 130);
        var btnImport = UIHelper.MakeFlowButton(L("import_excel"), UIHelper.AccentPurple, 130);
        var btnOrnek = UIHelper.MakeFlowButton(L("sample_file"), UIHelper.BtnDark, 110);

        btnYeni.Click += BtnYeni_Click;
        btnDuzenle.Click += BtnDuzenle_Click;
        btnSil.Click += BtnSil_Click;
        btnYaz.Click += (_, _) => Yazdir(); 
        btnExcel.Click += (_, _) => ExcelExport();
        btnImport.Click += (_, _) => ExcelImport();
        btnOrnek.Click += (_, _) => OrnekDosya();

        toolbar.Controls.AddRange(new Control[] { btnYeni, btnDuzenle, btnSil, btnYaz, btnExcel, btnImport, btnOrnek });

        // Data Grid
        dgv.Dock = DockStyle.Fill;
        UIHelper.StyleGrid(dgv, false);
        dgv.Columns.AddRange(
            new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "ID", Visible = false },
            new DataGridViewTextBoxColumn { Name = "CihazAdi", HeaderText = L("device_name", "Cihaz Adı"), FillWeight = 130 },
            new DataGridViewTextBoxColumn { Name = "SeriNumarasi", HeaderText = L("serial_number", "Seri Numarası"), FillWeight = 100 },
            new DataGridViewTextBoxColumn { Name = "Firma", HeaderText = L("company", "Firma"), FillWeight = 120 },
            new DataGridViewTextBoxColumn { Name = "Sorun", HeaderText = L("problem", "Sorun"), FillWeight = 150 },
            new DataGridViewTextBoxColumn { Name = "Sonuc", HeaderText = L("result", "Sonuç"), FillWeight = 150 },
            new DataGridViewTextBoxColumn { Name = "BakimTarihi", HeaderText = L("maintenance_date", "Bakım Tarihi"), FillWeight = 100 }
        );
        dgv.Columns["BakimTarihi"].DefaultCellStyle.Format = "dd.MM.yyyy";
        dgv.CellDoubleClick += (_, _) => btnDuzenle.PerformClick();
        // Status
        var pnlSt = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = UIHelper.BgPanel };
        lblInfo = new Label { Left = 20, Top = 8, AutoSize = true, Font = new Font("Segoe UI Semibold", 9f), ForeColor = UIHelper.TextMuted };
        pnlSt.Controls.Add(lblInfo);

        Controls.Add(dgv);
        Controls.Add(toolbar);
        Controls.Add(pnlF);
        Controls.Add(pnlH);
        Controls.Add(pnlSt);
    }


    private void Filtrele()
    {
        if (Program.DB == null) return;
        string? aramaKelimesi = string.IsNullOrWhiteSpace(txtArama.Text) ? null : txtArama.Text.Trim();
        
        _kayitlar = Program.DB.ServisKayitlariniGetir(dtpBas.Value.Date, dtpBit.Value.Date.AddDays(1), aramaKelimesi);
        
        dgv.Rows.Clear();
        foreach (var s in _kayitlar)
        {
            dgv.Rows.Add(s.Id, s.CihazAdi, s.SeriNumarasi, s.Firma, s.Sorun, s.Sonuc, s.BakimTarihi);
        }
        lblInfo.Text = $"{_kayitlar.Count} servis kaydı listelendi.";
    }

    private void Temizle()
    {
        dtpBas.Value = new DateTime(2024, 1, 1);
        dtpBit.Value = DateTime.Now;
        txtArama.Clear();
        Filtrele();
    }

    private ServisKaydi? GetSeciliKayit()
    {
        if (dgv.SelectedRows.Count == 0) return null;
        var cell = dgv.SelectedRows[0].Cells["Id"];
        if (cell?.Value == null) return null;
        int id = Convert.ToInt32(cell.Value);
        return _kayitlar.FirstOrDefault(x => x.Id == id);
    }

    private void BtnYeni_Click(object? sender, EventArgs e)
    {
        using var frm = new ServisEkleForm();
        if (frm.ShowDialog() == DialogResult.OK)
        {
            try
            {
                Program.DB?.ServisKaydiEkle(frm.Kayit);
                Filtrele();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, L("error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private void BtnDuzenle_Click(object? sender, EventArgs e)
    {
        var secili = GetSeciliKayit();
        if (secili == null)
        {
            MessageBox.Show(L("select_row_first"), L("warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var frm = new ServisEkleForm(secili);
        if (frm.ShowDialog() == DialogResult.OK)
        {
            try
            {
                Program.DB?.ServisKaydiGuncelle(frm.Kayit);
                Filtrele();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, L("error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private void BtnSil_Click(object? sender, EventArgs e)
    {
        var secili = GetSeciliKayit();
        if (secili == null)
        {
            MessageBox.Show(L("select_row_first"), L("warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show(L("confirm_service_delete", "Bu servis kaydı silinsin mi?"), L("confirm_delete_title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            try
            {
                Program.DB?.ServisKaydiSil(secili.Id);
                Filtrele();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, L("error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    // ═══ XLSX IMPORT ═══
    private void ExcelImport()
    {
        using var dlg = new OpenFileDialog { Filter = "Excel|*.xlsx", Title = L("import_excel") };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            using var wb = new ClosedXML.Excel.XLWorkbook(dlg.FileName);
            var ws = wb.Worksheet(1);
            var range = ws.RangeUsed();
            if (range == null) { MessageBox.Show(L("import_no_data")); return; }
            var rows = range.RowsUsed().Skip(1);
            
            var kayitlar = new List<ServisKaydi>();
            foreach (var row in rows)
            {
                var s = new ServisKaydi
                {
                    CihazAdi = row.Cell(1).GetString().Trim(),
                    SeriNumarasi = row.Cell(2).GetString().Trim(),
                    Firma = row.Cell(3).GetString().Trim(),
                    Sorun = row.Cell(4).GetString().Trim(),
                    Sonuc = row.Cell(5).GetString().Trim(),
                    BakimTarihi = row.Cell(6).GetDateTime()
                };

                if (!string.IsNullOrEmpty(s.CihazAdi))
                {
                    kayitlar.Add(s);
                }
            }
            if (kayitlar.Count > 0)
                Program.DB?.TopluServisKaydiEkle(kayitlar);
            Filtrele();
            MessageBox.Show(L("import_success", kayitlar.Count));
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    private void OrnekDosya()
    {
        using var dlg = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "Servis_Ornek.xlsx" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.AddWorksheet("Servis");
        string[] h = { "Cihaz Adı", "Seri No", "Firma", "Sorun", "Sonuç", "Tarih" };
        for (int i = 0; i < h.Length; i++) { ws.Cell(1, i + 1).Value = h[i]; ws.Cell(1, i+1).Style.Font.Bold = true; }
        ws.Cell(2, 1).Value = "Projeksiyon X"; ws.Cell(2, 2).Value = "SN12345"; ws.Cell(2, 3).Value = "ABC Ltd"; ws.Cell(2, 4).Value = "Lamba"; ws.Cell(2, 5).Value = "Değişti"; ws.Cell(2, 6).Value = DateTime.Now;
        ws.Columns().AdjustToContents();
        wb.SaveAs(dlg.FileName);
        MessageBox.Show(L("sample_file_created", dlg.FileName));
    }

    // ═══ XLSX EXPORT (ClosedXML) ═══
    private void ExcelExport()
    {
        using var dlg = new SaveFileDialog { Title = L("export_excel"), Filter = "Excel (*.xlsx)|*.xlsx", FileName = $"ServisKayitlari_{DateTime.Now:yyyyMMdd_HHmm}.xlsx" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.AddWorksheet("Servis Kayıtları");

            string[] headers = { L("device_name", "Cihaz Adı"), L("serial_number", "Seri Numarası"), L("company", "Firma"), L("problem", "Sorun"), L("result", "Sonuç"), L("maintenance_date", "Bakım Tarihi") };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(30, 60, 110);
                ws.Cell(1, i + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            }

            int row = 2;
            foreach (var s in _kayitlar)
            {
                ws.Cell(row, 1).Value = s.CihazAdi;
                ws.Cell(row, 2).Value = s.SeriNumarasi;
                ws.Cell(row, 3).Value = s.Firma;
                ws.Cell(row, 4).Value = s.Sorun;
                ws.Cell(row, 5).Value = s.Sonuc;
                ws.Cell(row, 6).Value = s.BakimTarihi.ToShortDateString();
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(dlg.FileName);
            MessageBox.Show(L("export_success", dlg.FileName), L("info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(L("export_error") + "\n" + ex.Message, L("error"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    // ═══ PRINT — Tarihe göre sıralı ═══
    private void Yazdir()
    {
        var sorted = _kayitlar.OrderByDescending(h => h.BakimTarihi).ThenByDescending(h => h.Id).ToList();

        string firma = Program.Settings.CompanyName; 
        var pd = new PrintDocument();
        pd.DefaultPageSettings.Landscape = true; 
        pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 1169, 827); 
        pd.DefaultPageSettings.Margins = new Margins(40, 40, 50, 50);

        using (var psd = new PageSetupDialog { Document = pd })
        {
            if (psd.ShowDialog() != DialogResult.OK) return;
        }
        
        int ps = 0; const int rpp = 28; int tp = Math.Max(1, (int)Math.Ceiling((double)sorted.Count / rpp));
        pd.BeginPrint += (_, _) => { ps = 0; };
        
        pd.PrintPage += (_, e) => {
            var g = e.Graphics!; float y = e.MarginBounds.Top, lm = e.MarginBounds.Left, pw = e.MarginBounds.Width;
            using var fT = new Font("Segoe UI", 13, FontStyle.Bold); using var fS = new Font("Segoe UI", 8); 
            using var fH = new Font("Segoe UI", 8f, FontStyle.Bold); using var fC = new Font("Segoe UI", 8f);
            using var br = new SolidBrush(Color.Black); using var brG = new SolidBrush(Color.Gray); 
            using var pen = new Pen(Color.FromArgb(180, 185, 200));
            
            if (ps == 0) 
            { 
                if (!string.IsNullOrWhiteSpace(firma)) { using var ff = new Font("Segoe UI", 9, FontStyle.Bold); g.DrawString(firma, ff, br, lm, y); y += 18; } 
                g.DrawString("SERVİS / BAKIM KAYITLARI RAPORU", fT, br, lm, y); y += 24; 
                g.DrawString(L("report_date", DateTime.Now.ToString("dd.MM.yyyy HH:mm")), fS, brG, lm, y); y += 16; 
                g.DrawLine(pen, lm, y, lm + pw, y); y += 6; 
            }
            
            float[] weights = { 180, 130, 160, 200, 200, 110 };
            float totalWeight = weights.Sum();
            float[] w = weights.Select(wt => (wt / totalWeight) * pw).ToArray();
            
            string[] hdr = { L("device_name", "Cihaz Adı"), L("serial_number", "Seri Numarası"), L("company", "Firma"), L("problem", "Sorun"), L("result", "Sonuç"), L("maintenance_date", "Bakım Tarihi") };
            using var brHd = new SolidBrush(Color.FromArgb(230, 235, 245)); g.FillRectangle(brHd, lm, y, pw, 18); float x = lm;
            for (int i = 0; i < hdr.Length; i++) { g.DrawString(hdr[i], fH, br, x + 2, y + 2); x += w[i]; } y += 20;
            
            int end = Math.Min(ps + rpp, sorted.Count); using var brAlt = new SolidBrush(Color.FromArgb(245, 247, 252));
            for (int i = ps; i < end; i++) 
            {
                var h = sorted[i]; if (i % 2 == 0) g.FillRectangle(brAlt, lm, y, pw, 18); x = lm;
                string[] cells = { h.CihazAdi, h.SeriNumarasi, h.Firma, h.Sorun, h.Sonuc, h.BakimTarihi.ToShortDateString() };
                for (int c = 0; c < cells.Length; c++) 
                { 
                    g.DrawString(cells[c], fC, br, new RectangleF(x + 2, y + 2, w[c] - 4, 16), new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }); 
                    x += w[c]; 
                }
                g.DrawLine(pen, lm, y + 18, lm + pw, y + 18); y += 19;
            }
            g.DrawString(L("total_records_page", sorted.Count, ps / rpp + 1, tp), fS, brG, lm, e.MarginBounds.Bottom - 8); 
            ps += rpp; 
            e.HasMorePages = ps < sorted.Count;
        };
        using var pv = new PrintPreviewDialog { Document = pd, Width = 1100, Height = 700, StartPosition = FormStartPosition.CenterParent }; 
        pv.ShowDialog(this);
    }
}
