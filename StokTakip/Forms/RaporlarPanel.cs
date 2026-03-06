using System.Data;
using System.Drawing.Printing;
using StokTakip.Models;
using static StokTakip.LocalizationManager;
using ScottPlot;
using ScottPlot.WinForms;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using FontStyle = System.Drawing.FontStyle;
using ContentAlignment = System.Drawing.ContentAlignment;

namespace StokTakip.Forms;

public class RaporlarPanel : UserControl
{
    private DateTimePicker dtpStart = new(), dtpEnd = new();
    private ComboBox cmbUser = new(), cmbDept = new(), cmbCategory = new();
    private FormsPlot plotChart = new();
    private DataGridView grid = new();
    private System.Windows.Forms.Label lblTotalInfo = new();
    private System.Windows.Forms.Label lblInfo = new();

    public RaporlarPanel()
    {
        BackColor = UIHelper.BgDark; Dock = DockStyle.Fill; DoubleBuffered = true;

        var pnlH = UIHelper.MakeHeader(L("reports"));

        // Filter Bar
        var pnlF = UIHelper.MakeToolbar(44); 
        pnlF.BackColor = UIHelper.BgPanel;
        pnlF.AutoSize = true;
        pnlF.WrapContents = true;
        pnlF.Padding = new Padding(8, 6, 8, 4);
        
        pnlF.Controls.Add(FL(L("start_date")));
        dtpStart = new DateTimePicker { Width = 110, Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy", Margin = new Padding(2), Value = DateTime.Today.AddDays(-30) };
        pnlF.Controls.Add(dtpStart);
        
        pnlF.Controls.Add(FL(L("end_date")));
        dtpEnd = new DateTimePicker { Width = 110, Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy", Margin = new Padding(2), Value = DateTime.Today };
        pnlF.Controls.Add(dtpEnd);

        pnlF.Controls.Add(FL(L("select_user") + ":"));
        cmbUser = new ComboBox { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(2) }; UIHelper.StyleComboBox(cmbUser);
        cmbUser.Items.Add(L("all")); 
        foreach (var u in Program.DB!.GetTeslimEdilenler()) cmbUser.Items.Add(u); 
        cmbUser.SelectedIndex = 0;
        pnlF.Controls.Add(cmbUser);
        
        pnlF.Controls.Add(FL(L("dept_filter")));
        cmbDept = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(2) }; UIHelper.StyleComboBox(cmbDept);
        cmbDept.Items.Add(L("all"));
        foreach (var d in Program.DB!.DepartmanlariGetir()) cmbDept.Items.Add(d);
        cmbDept.SelectedIndex = 0;
        pnlF.Controls.Add(cmbDept);

        pnlF.Controls.Add(FL(L("select_category") + ":"));
        cmbCategory = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(2) }; UIHelper.StyleComboBox(cmbCategory);
        var categories = Program.DB!.AltKartlariGetir().Where(k => !string.IsNullOrEmpty(k.Kategori)).Select(k => k.Kategori).Distinct().OrderBy(k => k).ToList();
        cmbCategory.Items.Add(L("all"));
        foreach (var c in categories) cmbCategory.Items.Add(c);
        cmbCategory.SelectedIndex = 0;
        pnlF.Controls.Add(cmbCategory);

        var btnGen = UIHelper.MakeFlowButton(L("generate_report"), UIHelper.AccentBlue, 90, 28);
        var btnClr = UIHelper.MakeFlowButton(L("clear_filter"), UIHelper.BtnDark, 70, 28);
        
        btnGen.Click += (_, _) => GenerateReport();
        btnClr.Click += (_, _) => { dtpStart.Value = DateTime.Today.AddDays(-30); dtpEnd.Value = DateTime.Today; cmbUser.SelectedIndex = 0; cmbDept.SelectedIndex = 0; cmbCategory.SelectedIndex = 0; GenerateReport(); };
        pnlF.Controls.AddRange(new Control[] { btnGen, btnClr });

        // Action Toolbar
        var pnlT = UIHelper.MakeToolbar(46); 
        pnlT.BackColor = UIHelper.BgPanel;
        pnlT.AutoSize = true;
        pnlT.WrapContents = true;
        var btnPrint = UIHelper.MakeFlowButton("🖨 " + L("print"), UIHelper.BtnMid, 100);
        btnPrint.Click += (_, _) => PrintReport();
        pnlT.Controls.Add(btnPrint);

        // Chart Area (Responsive TableLayoutPanel)
        var pnlTop = new TableLayoutPanel 
        { 
            Dock = DockStyle.Top, 
            Height = 340, 
            BackColor = UIHelper.BgDark, 
            Padding = new Padding(20, 15, 20, 10),
            ColumnCount = 2,
            RowCount = 1
        };
        pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75f)); // 75% for Chart
        pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f)); // 25% for Stats
        
        plotChart = new FormsPlot { Dock = DockStyle.Fill, BackColor = UIHelper.BgCard, Margin = new Padding(0, 0, 10, 0) };
        plotChart.Plot.FigureBackground.Color = ScottPlot.Color.FromColor(Color.FromArgb(20, 24, 34));
        plotChart.Plot.DataBackground.Color = ScottPlot.Color.FromColor(Color.FromArgb(20, 24, 34));
        plotChart.Plot.Axes.Color(ScottPlot.Color.FromColor(Color.FromArgb(140, 145, 160)));
        plotChart.Plot.Grid.LineColor = ScottPlot.Color.FromColor(Color.FromArgb(35, 40, 50));
        
        var cardTotal = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = UIHelper.BgCard, Padding = new Padding(15), Margin = new Padding(10, 0, 0, 0) };
        var lblT1 = new System.Windows.Forms.Label { Text = L("total_consumption").ToUpperInvariant(), Dock = DockStyle.Top, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = UIHelper.TextMuted, Height = 25 };
        lblTotalInfo = new System.Windows.Forms.Label { Text = "0", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 36, FontStyle.Bold), ForeColor = UIHelper.AccentPurple, TextAlign = ContentAlignment.MiddleCenter };
        cardTotal.Controls.Add(lblTotalInfo); cardTotal.Controls.Add(lblT1);

        pnlTop.Controls.Add(plotChart, 0, 0); 
        pnlTop.Controls.Add(cardTotal, 1, 0);

        // Data Grid 
        var pnlGrid = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 5, 20, 10) };
        var lblG = new System.Windows.Forms.Label { Text = L("movement_history"), Font=new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = UIHelper.TextPrimary, Dock=DockStyle.Top, Height = 26 };
        grid = new DataGridView { Dock = DockStyle.Fill }; UIHelper.StyleGrid(grid);
        grid.Columns.Add("Tarih", L("date")); grid.Columns["Tarih"]!.FillWeight = 85;
        grid.Columns["Tarih"]!.DefaultCellStyle.Format = "dd.MM.yyyy HH:mm";
        grid.Columns.Add("StokAd", L("stock_name")); grid.Columns["StokAd"]!.FillWeight = 200;
        grid.Columns.Add("Miktar", L("quantity")); grid.Columns["Miktar"]!.FillWeight = 80;
        grid.Columns.Add("Teslim", L("delivered_to")); grid.Columns["Teslim"]!.FillWeight = 160;
        grid.Columns.Add("Kategori", L("category")); grid.Columns["Kategori"]!.FillWeight = 120;
        grid.CellFormatting += (_, e) => {
            if (e.RowIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "Miktar")
            {
                if (e.CellStyle != null) { e.CellStyle.ForeColor = UIHelper.StokWarning; e.CellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold); }
            }
        };

        pnlGrid.Controls.Add(grid); pnlGrid.Controls.Add(lblG);

        // Status
        var pnlSt = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = UIHelper.BgPanel };
        lblInfo = new System.Windows.Forms.Label { Left = 20, Top = 8, AutoSize = true, Font = new Font("Segoe UI Semibold", 9f), ForeColor = UIHelper.TextMuted };
        pnlSt.Controls.Add(lblInfo);

        Controls.Add(pnlGrid); Controls.Add(pnlT); Controls.Add(pnlTop); Controls.Add(pnlF); Controls.Add(pnlH); Controls.Add(pnlSt);
        GenerateReport();
    }

    static System.Windows.Forms.Label FL(string t) => new System.Windows.Forms.Label { Text = t, AutoSize = true, Font = new Font("Segoe UI Semibold", 8.5f), ForeColor = UIHelper.TextSecondary, Margin = new Padding(4, 9, 2, 0) };

    private void GenerateReport()
    {
        // 1) Fetch data
        var start = dtpStart.Value.Date;
        var end = dtpEnd.Value.Date.AddDays(1).AddTicks(-1);
        string? user = cmbUser.SelectedIndex > 0 ? cmbUser.SelectedItem!.ToString() : null;
        string? dept = cmbDept.SelectedIndex > 0 ? cmbDept.SelectedItem!.ToString() : null;
        string? cat = cmbCategory.SelectedIndex > 0 ? cmbCategory.SelectedItem!.ToString() : null;

        // Fetch all movements between date. We only care about EXITS (consumption).
        var allMoves = Program.DB!.HareketleriGetir(null, start, end, null, "Cikis");
        var filteredData = allMoves.Where(h => 
            (user == null || h.TeslimEdilen == user) &&
            (dept == null || h.Departman == dept)
        ).ToList();

        // Join with StokKartlari to get Category and Ad.
        var kartlar = Program.DB!.AltKartlariGetir();
        
        var query = from h in filteredData
                    join k in kartlar on h.StokKartId equals k.Id
                    where (cat == null || k.Kategori == cat)
                    select new { Movement = h, Card = k };

        var finalRows = query.OrderBy(q => q.Movement.Tarih).ToList();

        // 2) Populate Grid
        grid.Rows.Clear();
        double totalConsumption = 0;
        foreach(var row in finalRows)
        {
            grid.Rows.Add(row.Movement.Tarih, row.Card.Ad, $"{UIHelper.FormatMiktar(row.Movement.Miktar)} [Ç]", row.Movement.TeslimEdilen, row.Card.Kategori);
            totalConsumption += row.Movement.Miktar;
        }

        lblTotalInfo.Text = UIHelper.FormatMiktar(totalConsumption);
        lblInfo.Text = finalRows.Count == 0 ? L("report_no_data") : L("records_info", finalRows.Count, 0);

        // 3) Plot Chart (Grouped by item)
        plotChart.Plot.Clear();
        if (finalRows.Count > 0)
        {
            var groupedByItem = finalRows.GroupBy(r => r.Card.Ad)
                                         .Select(g => new { Ad = g.Key, Miktar = g.Sum(r => r.Movement.Miktar) })
                                         .OrderByDescending(x => x.Miktar).Take(10).ToList(); // Top 10 consumed items

            double[] positions = Enumerable.Range(0, groupedByItem.Count).Select(x => (double)x).ToArray();
            double[] values = groupedByItem.Select(x => x.Miktar).ToArray();
            string[] labels = groupedByItem.Select(x => x.Ad).ToArray();

            var bars = plotChart.Plot.Add.Bars(positions, values);
            bars.Color = ScottPlot.Color.FromColor(UIHelper.AccentPurple);
            bars.LegendText = L("consumption_chart");
            
            // Format ticks for item names, keeping them short and steeply rotated for narrow width
            string[] shortLabels = labels.Select(l => l.Length > 16 ? l.Substring(0, 14) + ".." : l).ToArray();
            plotChart.Plot.Axes.Bottom.SetTicks(positions, shortLabels);
            plotChart.Plot.Axes.Bottom.TickLabelStyle.Rotation = -60;
            plotChart.Plot.Axes.Bottom.TickLabelStyle.Alignment = Alignment.MiddleRight;
            plotChart.Plot.Axes.Bottom.TickLabelStyle.FontSize = 10.5f;
            
            string chartTitle = string.IsNullOrEmpty(user) 
                                ? (string.IsNullOrEmpty(dept) ? "En Çok Tüketilen Ürünler" : $"{dept} Departmanı Tüketimi") 
                                : $"{user} Tüketimi";
                                
            plotChart.Plot.Axes.Title.Label.Text = chartTitle;
            plotChart.Plot.Axes.Title.Label.ForeColor = ScottPlot.Color.FromColor(Color.White);
            plotChart.Plot.Axes.Left.TickLabelStyle.ForeColor = ScottPlot.Color.FromColor(Color.LightGray);
            plotChart.Plot.Axes.Bottom.TickLabelStyle.ForeColor = ScottPlot.Color.FromColor(Color.LightGray);
        }
        else
        {
             plotChart.Plot.Axes.Title.Label.Text = L("report_no_data");
             plotChart.Plot.Axes.Title.Label.ForeColor = ScottPlot.Color.FromColor(Color.Gray);
        }

        plotChart.Refresh();
    }

    private void PrintReport()
    {
        if (grid.Rows.Count == 0) { MessageBox.Show(L("report_no_data"), L("warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        string firma = Program.Settings.CompanyName; var pd = new PrintDocument();
        pd.DefaultPageSettings.Landscape = true; pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 1169, 827); pd.DefaultPageSettings.Margins = new Margins(40, 40, 50, 50);
        
        int ps = 0; const int rpp = 28; int tp = Math.Max(1, (int)Math.Ceiling((double)grid.Rows.Count / rpp));
        
        string titleStr = L("reports") + " - " + plotChart.Plot.Axes.Title.Label.Text;
        string reportDateInfo = dtpStart.Value.ToString("dd.MM.yyyy") + " - " + dtpEnd.Value.ToString("dd.MM.yyyy");

        pd.PrintPage += (_, e) => {
            var g = e.Graphics!; float y = e.MarginBounds.Top, lm = e.MarginBounds.Left, pw = e.MarginBounds.Width;
            using var fT = new Font("Segoe UI", 13, FontStyle.Bold); using var fS = new Font("Segoe UI", 8); 
            using var fH = new Font("Segoe UI", 7.5f, FontStyle.Bold); using var fC = new Font("Segoe UI", 7.5f);
            using var fTotal = new Font("Segoe UI", 12, FontStyle.Bold);
            using var br = new SolidBrush(Color.Black); using var brG = new SolidBrush(Color.Gray); 
            using var pen = new Pen(Color.FromArgb(180, 185, 200));
            
            if (ps == 0) { 
                if (!string.IsNullOrWhiteSpace(firma)) { using var ff = new Font("Segoe UI", 9, FontStyle.Bold); g.DrawString(firma, ff, br, lm, y); y += 18; } 
                g.DrawString(titleStr, fT, br, lm, y); y += 24; 
                g.DrawString(L("report_date", reportDateInfo), fS, brG, lm, y); y += 16; 
                g.DrawLine(pen, lm, y, lm + pw, y); y += 6; 
            }
            
            float[] w = { 100, 300, 100, 200, 0 }; float u = 0; foreach (var ww in w) u += ww; w[^1] = pw - u;
            string[] hdr = { L("date"), L("stock_name"), L("quantity"), L("delivered_to"), L("category") };
            using var brHd = new SolidBrush(Color.FromArgb(230, 235, 245)); g.FillRectangle(brHd, lm, y, pw, 16); float x = lm;
            for (int i = 0; i < hdr.Length; i++) { g.DrawString(hdr[i], fH, br, x + 2, y + 2); x += w[i]; } y += 18;
            
            int end = Math.Min(ps + rpp, grid.Rows.Count); using var brAlt = new SolidBrush(Color.FromArgb(245, 247, 252));
            for (int i = ps; i < end; i++) {
                var row = grid.Rows[i]; 
                if (i % 2 == 0) g.FillRectangle(brAlt, lm, y, pw, 15); 
                x = lm; 
                
                string[] cells = { 
                    Convert.ToDateTime(row.Cells[0].Value).ToString("dd.MM.yyyy HH:mm"), 
                    row.Cells[1].Value?.ToString() ?? "", 
                    row.Cells[2].Value?.ToString() ?? "", 
                    row.Cells[3].Value?.ToString() ?? "", 
                    row.Cells[4].Value?.ToString() ?? "" 
                };
                
                for (int c = 0; c < cells.Length; c++) { 
                    g.DrawString(cells[c], fC, br, new RectangleF(x + 2, y + 1, w[c] - 4, 14), new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }); 
                    x += w[c]; 
                }
                g.DrawLine(pen, lm, y + 15, lm + pw, y + 15); y += 16;
            }
            
            if (ps + rpp >= grid.Rows.Count)
            {
                y += 10;
                string totalStr = L("total_consumption") + ": " + lblTotalInfo.Text;
                g.DrawString(totalStr, fTotal, new SolidBrush(Color.DarkBlue), lm, y);
            }

            g.DrawString(L("total_records_page", grid.Rows.Count, ps / rpp + 1, tp), fS, brG, lm, e.MarginBounds.Bottom - 8); ps += rpp; e.HasMorePages = ps < grid.Rows.Count;
        };
        using var pv = new PrintPreviewDialog { Document = pd, Width = 1100, Height = 700, StartPosition = FormStartPosition.CenterParent }; pv.ShowDialog(this);
    }
}
