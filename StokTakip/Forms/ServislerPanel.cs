using System.Drawing.Printing;
using StokTakip.Data;
using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class ServislerPanel : Panel
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

        // Filter Bar
        var pnlF = UIHelper.MakeToolbar(40); 
        pnlF.BackColor = UIHelper.BgPanel;
        
        pnlF.Controls.Add(FL(L("date_filter")));
        dtpBas = new DateTimePicker { Width = 120, Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy", Margin = new Padding(2), Value = DateTime.Now.AddMonths(-1) };
        pnlF.Controls.Add(dtpBas); 
        pnlF.Controls.Add(FL("-"));
        dtpBit = new DateTimePicker { Width = 120, Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy", Margin = new Padding(2) };
        pnlF.Controls.Add(dtpBit);

        pnlF.Controls.Add(FL("Arama:"));
        txtArama = UIHelper.MakeSearchBox("Cihaz adı, seri no veya açıklama...", 250);
        txtArama.Margin = new Padding(4, 3, 8, 2);
        txtArama.TextChanged += (_, _) => Filtrele();
        pnlF.Controls.Add(txtArama);

        var btnFil = UIHelper.MakeFlowButton(L("filter"), UIHelper.AccentBlue, 80, 28);
        var btnClr = UIHelper.MakeFlowButton(L("clear_filter"), UIHelper.BtnDark, 80, 28);
        btnFil.Click += (_, _) => Filtrele(); 
        btnClr.Click += (_, _) => Temizle();
        pnlF.Controls.AddRange(new Control[] { btnFil, btnClr });

        // Action Toolbar
        var toolbar = UIHelper.MakeToolbar();
        var btnYeni = UIHelper.MakeFlowButton(L("new_service_record", "＋ Yeni Kayıt"), UIHelper.AccentBlue, 140);
        var btnDuzenle = UIHelper.MakeFlowButton(L("edit"), UIHelper.AccentOrange, 100);
        var btnSil = UIHelper.MakeFlowButton(L("delete"), UIHelper.AccentRed, 90);
        var btnYaz = UIHelper.MakeFlowButton(L("print"), UIHelper.BtnMid, 90);
        var btnExcel = UIHelper.MakeFlowButton(L("export_excel"), UIHelper.AccentCyan, 130);

        btnYeni.Click += BtnYeni_Click;
        btnDuzenle.Click += BtnDuzenle_Click;
        btnSil.Click += BtnSil_Click;
        btnYaz.Click += (_, _) => Yazdir(); 
        btnExcel.Click += (_, _) => ExcelExport();

        toolbar.Controls.AddRange(new Control[] { btnYeni, btnDuzenle, btnSil, new Label { Width = 10 }, btnYaz, btnExcel });

        // Data Grid
        dgv.Dock = DockStyle.Fill;
        UIHelper.StyleGrid(dgv, false);
        dgv.Columns.AddRange(
            new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "ID", Visible = false },
            new DataGridViewTextBoxColumn { Name = "CihazAdi", HeaderText = L("device_name", "Cihaz Adı"), FillWeight = 130 },
            new DataGridViewTextBoxColumn { Name = "SeriNumarasi", HeaderText = L("serial_number", "Seri Numarası"), FillWeight = 100 },
            new DataGridViewTextBoxColumn { Name = "BakimTarihi", HeaderText = L("maintenance_date", "Bakım Tarihi"), FillWeight = 100 },
            new DataGridViewTextBoxColumn { Name = "Aciklama", HeaderText = L("description", "Açıklama"), FillWeight = 250 }
        );
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

    static Label FL(string t) => new Label { Text = t, AutoSize = true, Font = new Font("Segoe UI Semibold", 8.5f), ForeColor = UIHelper.TextSecondary, Margin = new Padding(4, 9, 2, 0) };

    private void Filtrele()
    {
        if (Program.DB == null) return;
        string? aramaKelimesi = string.IsNullOrWhiteSpace(txtArama.Text) ? null : txtArama.Text.Trim();
        
        _kayitlar = Program.DB.ServisKayitlariniGetir(dtpBas.Value.Date, dtpBit.Value.Date.AddDays(1), aramaKelimesi);
        
        dgv.Rows.Clear();
        foreach (var s in _kayitlar)
        {
            dgv.Rows.Add(s.Id, s.CihazAdi, s.SeriNumarasi, s.BakimTarihi.ToShortDateString(), s.Aciklama);
        }
        lblInfo.Text = $"{_kayitlar.Count} servis kaydı listelendi.";
    }

    private void Temizle()
    {
        dtpBas.Value = DateTime.Now.AddMonths(-1);
        dtpBit.Value = DateTime.Now;
        txtArama.Clear();
        Filtrele();
    }

    private ServisKaydi? GetSeciliKayit()
    {
        if (dgv.SelectedRows.Count == 0) return null;
        int id = Convert.ToInt32(dgv.SelectedRows[0].Cells["Id"].Value);
        return _kayitlar.FirstOrDefault(x => x.Id == id);
    }

    private void BtnYeni_Click(object? sender, EventArgs e)
    {
        using var frm = new ServisEkleForm();
        if (frm.ShowDialog() == DialogResult.OK)
        {
            Program.DB?.ServisKaydiEkle(frm.Kayit);
            Filtrele();
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
            Program.DB?.ServisKaydiGuncelle(frm.Kayit);
            Filtrele();
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
            Program.DB?.ServisKaydiSil(secili.Id);
            Filtrele();
        }
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

            string[] headers = { L("device_name", "Cihaz Adı"), L("serial_number", "Seri Numarası"), L("maintenance_date", "Bakım Tarihi"), L("description", "Açıklama") };
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
                ws.Cell(row, 3).Value = s.BakimTarihi.ToShortDateString();
                ws.Cell(row, 4).Value = s.Aciklama;
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
        
        int ps = 0; const int rpp = 28; int tp = Math.Max(1, (int)Math.Ceiling((double)sorted.Count / rpp));
        
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
            
            float[] w = { 200, 150, 100, 0 }; 
            float u = 0; foreach (var ww in w) u += ww; w[^1] = pw - u;
            
            string[] hdr = { L("device_name", "Cihaz Adı"), L("serial_number", "Seri Numarası"), L("maintenance_date", "Bakım Tarihi"), L("description", "Açıklama") };
            using var brHd = new SolidBrush(Color.FromArgb(230, 235, 245)); g.FillRectangle(brHd, lm, y, pw, 18); float x = lm;
            for (int i = 0; i < hdr.Length; i++) { g.DrawString(hdr[i], fH, br, x + 2, y + 2); x += w[i]; } y += 20;
            
            int end = Math.Min(ps + rpp, sorted.Count); using var brAlt = new SolidBrush(Color.FromArgb(245, 247, 252));
            for (int i = ps; i < end; i++) 
            {
                var h = sorted[i]; if (i % 2 == 0) g.FillRectangle(brAlt, lm, y, pw, 18); x = lm;
                string[] cells = { h.CihazAdi, h.SeriNumarasi, h.BakimTarihi.ToShortDateString(), h.Aciklama };
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
