using System.Drawing.Printing;
using System.Drawing.Drawing2D;
using StokTakip.Models;
using static StokTakip.LocalizationManager;
using ScottPlot;
using ScottPlot.WinForms;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using FontStyle = System.Drawing.FontStyle;
using ContentAlignment = System.Drawing.ContentAlignment;

namespace StokTakip.Forms;

public sealed class RaporlarPanel : UserControl
{
    private DateTimePicker dtpStart = new(), dtpEnd = new();
    private ComboBox cmbUser = new(), cmbDept = new(), cmbCategory = new();
    private FormsPlot plotChart = new();
    private DataGridView grid = new();
    private System.Windows.Forms.Label lblTotalInfo    = new();
    private System.Windows.Forms.Label lblUniqueInfo   = new();
    private System.Windows.Forms.Label lblInfo         = new();
    private Button btnGen = new(), btnClr = new(), btnPrint = new();
    private CancellationTokenSource? reportCts;
    private int reportVersion;
    private string[] chartLabels = Array.Empty<string>();
    private double[] chartValues = Array.Empty<double>();
    private readonly ToolTip chartTip = new();
    private int chartHoverIndex = -1;
    private static readonly Font QtyFont = new("Segoe UI", 9.5f, FontStyle.Bold);

    public RaporlarPanel()
    {
        BackColor = UIHelper.BgDark; Dock = DockStyle.Fill; DoubleBuffered = true;

        // ═══ HEADER ═══
        var pnlH = UIHelper.MakeHeader(L("reports"), L("reports_subtitle"));

        // ═══ FILTER BAR (Standart) ═══
        dtpStart = new DateTimePicker { Value = DateTime.Today.AddDays(-30) };
        dtpEnd = new DateTimePicker { Value = DateTime.Today };
        dtpStart.ValueChanged += (_, _) => { if (dtpStart.Value.Date > dtpEnd.Value.Date) dtpEnd.Value = dtpStart.Value.Date; };
        dtpEnd.ValueChanged += (_, _) => { if (dtpEnd.Value.Date < dtpStart.Value.Date) dtpStart.Value = dtpEnd.Value.Date; };

        cmbUser = MakeCombo(160); cmbUser.Items.Add(L("all"));
        foreach (var u in Program.DB!.GetTeslimEdilenler()) cmbUser.Items.Add(u);
        cmbUser.SelectedIndex = 0;

        cmbDept = MakeCombo(130); cmbDept.Items.Add(L("all"));
        foreach (var d in Program.DB!.DepartmanlariGetir()) cmbDept.Items.Add(d);
        cmbDept.SelectedIndex = 0;

        cmbCategory = MakeCombo(130); cmbCategory.Items.Add(L("all"));
        foreach (var c in Program.DB!.AltKartlariGetir().Where(k => !string.IsNullOrEmpty(k.Kategori)).Select(k => k.Kategori).Distinct().OrderBy(k => k)) cmbCategory.Items.Add(c);
        cmbCategory.SelectedIndex = 0;

        btnGen = UIHelper.MakeFlowButton(L("generate_report"), UIHelper.AccentBlue, 100, 30);
        btnClr = UIHelper.MakeFlowButton(L("clear_filter"), UIHelper.BtnDark, 90, 30);
        btnGen.Click += (_, _) => GenerateReport();
        btnClr.Click += (_, _) => { dtpStart.Value = DateTime.Today.AddDays(-30); dtpEnd.Value = DateTime.Today; cmbUser.SelectedIndex = 0; cmbDept.SelectedIndex = 0; cmbCategory.SelectedIndex = 0; GenerateReport(); };

        var pnlFWrap = UIHelper.MakeFilterBar();
        var flow = UIHelper.GetFilterFlow(pnlFWrap);

        var cellDate = UIHelper.MakeDateRangeCell(L("date_filter"), dtpStart, dtpEnd);
        var cellUser = UIHelper.MakeFilterCell(L("select_user"), cmbUser, 160);
        var cellDept = UIHelper.MakeFilterCell(L("dept_filter"), cmbDept, 130);
        var cellCat  = UIHelper.MakeFilterCell(L("select_category"), cmbCategory, 130);
        var cellBtns = UIHelper.MakeFilterButtons(btnGen, btnClr);

        flow.Controls.AddRange(new Control[] { cellDate, cellUser, cellDept, cellCat, cellBtns });

        // ═══ ACTION TOOLBAR ═══
        var pnlT = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 40, BackColor = UIHelper.BgDark,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(16, 4, 16, 4), WrapContents = false
        };
        var btnPrint = UIHelper.MakeFlowButton(L("print"), UIHelper.BtnMid, 110, 28);
        btnPrint.Margin = new Padding(0, 2, 6, 2);
        btnPrint.Click += (_, _) => PrintReport();
        this.btnPrint = btnPrint;
        pnlT.Controls.Add(btnPrint);

        // ═══ CHART + STAT CARDS AREA ═══
        var pnlTop = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 300,
            ColumnCount = 2, RowCount = 1,
            BackColor = UIHelper.BgDark,
            Padding = new Padding(16, 8, 16, 8)
        };
        pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72f));
        pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));

        // Chart
        plotChart = new FormsPlot
        {
            Dock = DockStyle.Fill, BackColor = UIHelper.BgCard,
            Margin = new Padding(0, 0, 8, 0)
        };
        plotChart.Plot.FigureBackground.Color = ScottPlot.Color.FromColor(Color.FromArgb(20, 24, 34));
        plotChart.Plot.DataBackground.Color   = ScottPlot.Color.FromColor(Color.FromArgb(20, 24, 34));
        plotChart.Plot.Axes.Color(ScottPlot.Color.FromColor(Color.FromArgb(140, 145, 160)));
        plotChart.Plot.Grid.LineColor = ScottPlot.Color.FromColor(Color.FromArgb(35, 40, 50));
        chartTip.BackColor = UIHelper.BgCard;
        chartTip.ForeColor = UIHelper.TextPrimary;
        chartTip.InitialDelay = 200;
        chartTip.ReshowDelay = 100;
        chartTip.AutoPopDelay = 1500;
        chartTip.ShowAlways = true;
        plotChart.MouseMove += PlotChart_MouseMove;
        plotChart.MouseLeave += (_, _) => { chartTip.Hide(plotChart); chartHoverIndex = -1; };

        // Stat cards (right column — stacked)
        var pnlStats = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UIHelper.BgDark,
            Margin = new Padding(8, 0, 0, 0),
            Padding = new Padding(0),
            ColumnCount = 1,
            RowCount = 2
        };
        pnlStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        pnlStats.RowStyles.Add(new RowStyle(SizeType.Absolute, 148f));
        pnlStats.RowStyles.Add(new RowStyle(SizeType.Absolute, 148f));

        var cardTotal  = MakeReportStatCard(L("total_consumption"), UIHelper.AccentPurple, 0);
        var cardUnique = MakeReportStatCard(L("unique_items"),       UIHelper.AccentCyan,   148);
        lblTotalInfo  = (System.Windows.Forms.Label)cardTotal.Controls["val"]!;
        lblUniqueInfo = (System.Windows.Forms.Label)cardUnique.Controls["val"]!;
        lblTotalInfo.Text  = "0";
        lblUniqueInfo.Text = "0";
        pnlStats.Controls.Add(cardTotal, 0, 0);
        pnlStats.Controls.Add(cardUnique, 0, 1);

        pnlTop.Controls.Add(plotChart, 0, 0);
        pnlTop.Controls.Add(pnlStats,  1, 0);

        // ═══ SECTION LABEL + GRID ═══
        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 4, 16, 8),
            BackColor = UIHelper.BgDark
        };

        var pnlGridHeader = new Panel
        {
            Dock = DockStyle.Top, Height = 36,
            BackColor = UIHelper.BgDark
        };
        var lblG = UIHelper.MakeIconTitle(
            L("movement_history"),
            UIHelper.TextPrimary,
            new Font("Segoe UI", 10.5f, FontStyle.Bold),
            left: 0, top: 8, gap: 6, iconSize: 12f, iconTop: 1, textTop: 0);
        pnlGridHeader.Controls.Add(lblG);

        grid = new DataGridView { Dock = DockStyle.Fill };
        UIHelper.StyleGrid(grid);

        void AddCol(string name, string header, int weight)
        {
            grid.Columns.Add(name, header);
            grid.Columns[name]!.FillWeight = weight;
        }
        AddCol("Tarih",    L("date"),         85);
        AddCol("StokAd",   L("stock_name"),   200);
        AddCol("Miktar",   L("quantity"),      75);
        AddCol("Teslim",   L("delivered_to"), 155);
        AddCol("Kategori", L("category"),     115);
        grid.Columns["Tarih"]!.DefaultCellStyle.Format = "dd.MM.yyyy HH:mm";
        var qtyCol = grid.Columns["Miktar"]!;
        qtyCol.DefaultCellStyle.ForeColor = UIHelper.StokWarning;
        qtyCol.DefaultCellStyle.Font = QtyFont;

        pnlGrid.Controls.Add(grid);
        pnlGrid.Controls.Add(pnlGridHeader);

        // ═══ STATUS BAR ═══
        var pnlSt = new Panel
        {
            Dock = DockStyle.Bottom, Height = 30,
            BackColor = UIHelper.BgPanel
        };
        // left accent line
        pnlSt.Controls.Add(new Panel { Left = 0, Top = 0, Width = 3, Height = 30, BackColor = UIHelper.AccentBlue, Dock = DockStyle.Left });
        lblInfo = new System.Windows.Forms.Label
        {
            Left = 12, Top = 7, AutoSize = true,
            Font = new Font("Segoe UI Semibold", 8.5f),
            ForeColor = UIHelper.TextSecondary
        };
        pnlSt.Controls.Add(lblInfo);

        Controls.Add(pnlGrid);
        Controls.Add(pnlTop);
        Controls.Add(pnlT);
        Controls.Add(pnlFWrap);
        Controls.Add(pnlH);
        Controls.Add(pnlSt);

        GenerateReport();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ComboBox MakeCombo(int width)
    {
        var c = new ComboBox { Width = width, DropDownStyle = ComboBoxStyle.DropDownList };
        UIHelper.StyleComboBox(c);
        return c;
    }

    /// <summary>Custom-painted mini stat card for the report panel right column.</summary>
    private static Panel MakeReportStatCard(string title, Color accent, int top)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UIHelper.BgCard,
            Margin = new Padding(0, 0, 0, 12)
        };

        // Accent left bar
        card.Controls.Add(new Panel
        {
            Dock = DockStyle.Left, Width = 4,
            BackColor = accent
        });

        // Title
        card.Controls.Add(new System.Windows.Forms.Label
        {
            Name = "ttl",
            Text = title.ToUpperTr(),
            Left = 14, Top = 12, AutoSize = true,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = UIHelper.TextMuted
        });

        // Value
        var lblVal = new System.Windows.Forms.Label
        {
            Name = "val",
            Text = "0",
            Left = 12, Top = 34,
            Width = 200, Height = 60,
            Font = new Font("Segoe UI", 34, FontStyle.Bold),
            ForeColor = accent,
            TextAlign = ContentAlignment.MiddleLeft
        };
        card.Controls.Add(lblVal);

        // Hover effect
        card.MouseEnter += (_, _) => card.BackColor = UIHelper.BgHover;
        card.MouseLeave += (_, _) => card.BackColor = UIHelper.BgCard;
        foreach (Control c in card.Controls)
        {
            c.MouseEnter += (_, _) => card.BackColor = UIHelper.BgHover;
            c.MouseLeave += (_, _) => card.BackColor = UIHelper.BgCard;
        }

        return card;
    }

    private sealed class ReportRow
    {
        public DateTime Tarih { get; init; }
        public string StokAd { get; init; } = "";
        public double Miktar { get; init; }
        public string Teslim { get; init; } = "";
        public string? Kategori { get; init; }
        public int CardId { get; init; }
    }

    private sealed class ReportData
    {
        public List<ReportRow> Rows { get; } = new();
        public double TotalConsumption { get; set; }
        public int UniqueItems { get; set; }
        public List<(string Name, double Total)> TopItems { get; } = new();
    }

    // ── Report Generation ──────────────────────────────────────────────────────

    private async void GenerateReport()
    {
        reportCts?.Cancel();
        reportCts?.Dispose();
        var cts = new CancellationTokenSource();
        reportCts = cts;
        var token = cts.Token;
        int version = Interlocked.Increment(ref reportVersion);

        var start = dtpStart.Value.Date;
        var end   = dtpEnd.Value.Date.AddDays(1).AddTicks(-1);
        string? user = cmbUser.SelectedIndex    > 0 ? cmbUser.SelectedItem!.ToString()    : null;
        string? dept = cmbDept.SelectedIndex    > 0 ? cmbDept.SelectedItem!.ToString()    : null;
        string? cat  = cmbCategory.SelectedIndex > 0 ? cmbCategory.SelectedItem!.ToString() : null;

        SetBusy(true);
        try
        {
            var data = await Task.Run(() => BuildReportData(start, end, user, dept, cat, token), token);
            if (token.IsCancellationRequested || version != reportVersion)
                return;

            ApplyReportData(data, user, dept);
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        finally
        {
            if (version == reportVersion)
                SetBusy(false);
        }
    }

    private ReportData BuildReportData(DateTime start, DateTime end, string? user, string? dept, string? cat, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var allMoves = Program.DB!.HareketleriGetir(null, start, end, null, nameof(HareketTuru.Cikis));
        token.ThrowIfCancellationRequested();
        var filtered = allMoves.Where(h =>
            (user == null || h.TeslimEdilen == user) &&
            (dept == null || h.Departman    == dept)
        ).ToList();

        var kartlar = Program.DB!.AltKartlariGetir();
        var kartMap = kartlar.ToDictionary(k => k.Id);

        var rows = new List<ReportRow>(filtered.Count);
        var totals = new Dictionary<string, double>();
        var unique = new HashSet<int>();
        double total = 0;

        foreach (var h in filtered)
        {
            token.ThrowIfCancellationRequested();
            if (!kartMap.TryGetValue(h.StokKartId, out var k)) continue;
            if (cat != null && k.Kategori != cat) continue;

            rows.Add(new ReportRow
            {
                Tarih = h.Tarih,
                StokAd = k.Ad,
                Miktar = h.Miktar,
                Teslim = h.TeslimEdilen ?? "",
                Kategori = k.Kategori,
                CardId = k.Id
            });

            total += h.Miktar;
            unique.Add(k.Id);
            totals[k.Ad] = totals.TryGetValue(k.Ad, out var cur) ? cur + h.Miktar : h.Miktar;
        }

        rows.Sort((a, b) => b.Tarih.CompareTo(a.Tarih)); // Newest first

        var data = new ReportData
        {
            TotalConsumption = total,
            UniqueItems = unique.Count
        };
        data.Rows.AddRange(rows);

        foreach (var kvp in totals.OrderByDescending(k => k.Value).Take(10))
            data.TopItems.Add((kvp.Key, kvp.Value));

        return data;
    }

    private void ApplyReportData(ReportData data, string? user, string? dept)
    {
        var prevAutoSize = grid.AutoSizeColumnsMode;
        grid.SuspendLayout();
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        try
        {
            grid.Rows.Clear();
            foreach (var row in data.Rows)
            {
                grid.Rows.Add(
                    row.Tarih,
                    row.StokAd,
                    $"{UIHelper.FormatMiktar(row.Miktar)} [Ç]",
                    row.Teslim,
                    row.Kategori);
            }

            lblTotalInfo.Text  = UIHelper.FormatMiktar(data.TotalConsumption);
            lblUniqueInfo.Text = data.UniqueItems.ToString();
            lblInfo.Text = data.Rows.Count == 0
                ? L("report_no_data")
                : L("records_info", data.Rows.Count, 0);

            UpdateChart(data.TopItems, user, dept);
        }
        finally
        {
            grid.AutoSizeColumnsMode = prevAutoSize;
            grid.ClearSelection();
            grid.ResumeLayout();
        }
    }

    private void UpdateChart(List<(string Name, double Total)> items, string? user, string? dept)
    {
        plotChart.Plot.Clear();
        chartHoverIndex = -1;
        chartTip.Hide(plotChart);
        chartLabels = Array.Empty<string>();
        chartValues = Array.Empty<double>();

        if (items.Count > 0)
        {
            chartLabels = items.Select(x => x.Name).ToArray();
            chartValues = items.Select(x => x.Total).ToArray();
            double[] pos = Enumerable.Range(0, items.Count).Select(x => (double)x).ToArray();

            var bars = plotChart.Plot.Add.Bars(pos, chartValues);
            bars.Color      = ScottPlot.Color.FromColor(UIHelper.AccentPurple);
            bars.LegendText = L("consumption_chart");
            foreach (var b in bars.Bars) b.Size = 0.6;

            int maxChars = GetMaxLabelChars(items.Count);
            string[] shortLabels = BuildShortLabels(chartLabels, maxChars);

            plotChart.Plot.Axes.Bottom.SetTicks(pos, shortLabels);
            plotChart.Plot.Axes.Bottom.TickLabelStyle.Rotation  = -45;
            plotChart.Plot.Axes.Bottom.TickLabelStyle.Alignment = Alignment.MiddleRight;
            plotChart.Plot.Axes.Bottom.TickLabelStyle.FontSize   = 10f;
            plotChart.Plot.Axes.Left.TickLabelStyle.ForeColor    = ScottPlot.Color.FromColor(Color.LightGray);
            plotChart.Plot.Axes.Bottom.TickLabelStyle.ForeColor  = ScottPlot.Color.FromColor(Color.LightGray);

            string chartTitle = user != null ? $"{user} {L("consumption_chart")}"
                              : dept != null ? $"{dept} {L("consumption_chart")}"
                              : L("top_consumed_items");

            plotChart.Plot.Axes.Title.Label.Text     = chartTitle;
            plotChart.Plot.Axes.Title.Label.ForeColor = ScottPlot.Color.FromColor(Color.White);
        }
        else
        {
            plotChart.Plot.Axes.Title.Label.Text     = L("report_no_data");
            plotChart.Plot.Axes.Title.Label.ForeColor = ScottPlot.Color.FromColor(Color.Gray);
            plotChart.Plot.Axes.Bottom.SetTicks(Array.Empty<double>(), Array.Empty<string>());
        }

        plotChart.Refresh();
    }

    private int GetMaxLabelChars(int itemCount)
    {
        if (itemCount <= 0) return 12;
        int plotWidth = Math.Max(1, plotChart.Width);
        int pxPerBar = Math.Max(1, plotWidth / itemCount);
        int maxChars = pxPerBar / 7;
        return Math.Clamp(maxChars, 8, 18);
    }

    private static string[] BuildShortLabels(string[] labels, int maxChars)
    {
        if (maxChars < 4) maxChars = 4;
        return labels
            .Select(l => l.Length > maxChars ? l.Substring(0, maxChars - 2) + ".." : l)
            .ToArray();
    }

    private void PlotChart_MouseMove(object? sender, MouseEventArgs e)
    {
        if (chartLabels.Length == 0 || chartValues.Length != chartLabels.Length)
            return;

        try
        {
            var coords = plotChart.Plot.GetCoordinates(new Pixel(e.X, e.Y));
            int idx = (int)Math.Round(coords.X);
            if (idx < 0 || idx >= chartLabels.Length)
            {
                if (chartHoverIndex != -1)
                {
                    chartTip.Hide(plotChart);
                    chartHoverIndex = -1;
                }
                return;
            }

            if (idx != chartHoverIndex)
            {
                chartHoverIndex = idx;
                string text = $"{chartLabels[idx]}: {UIHelper.FormatMiktar(chartValues[idx])}";
                chartTip.Show(text, plotChart, e.Location.X + 12, e.Location.Y + 12, 1500);
            }
        }
        catch
        {
            // ignore plotting coordinate errors
        }
    }

    // ── Print ──────────────────────────────────────────────────────────────────

    private void SetBusy(bool busy)
    {
        UseWaitCursor = busy;
        btnGen.Enabled = !busy;
        btnClr.Enabled = !busy;
        btnPrint.Enabled = !busy;
        dtpStart.Enabled = !busy;
        dtpEnd.Enabled = !busy;
        cmbUser.Enabled = !busy;
        cmbDept.Enabled = !busy;
        cmbCategory.Enabled = !busy;
    }

    private void PrintReport()
    {
        if (grid.Rows.Count == 0)
        {
            MessageBox.Show(L("report_no_data"), L("warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string firma = Program.Settings.CompanyName;
        var pd = new PrintDocument();
        pd.DefaultPageSettings.Landscape  = true;
        pd.DefaultPageSettings.PaperSize  = new PaperSize("A4", 1169, 827);
        pd.DefaultPageSettings.Margins    = new Margins(40, 40, 50, 50);

        using (var psd = new PageSetupDialog { Document = pd })
        {
            if (psd.ShowDialog() != DialogResult.OK) return;
        }

        int ps = 0; const int rpp = 28;
        int tp = Math.Max(1, (int)Math.Ceiling((double)grid.Rows.Count / rpp));

        string titleStr      = L("reports") + " — " + plotChart.Plot.Axes.Title.Label.Text;
        string reportDateInfo = dtpStart.Value.ToString("dd.MM.yyyy") + " – " + dtpEnd.Value.ToString("dd.MM.yyyy");

        pd.PrintPage += (_, e) =>
        {
            var g  = e.Graphics!;
            float y = e.MarginBounds.Top, lm = e.MarginBounds.Left, pw = e.MarginBounds.Width;

            using var fT     = new Font("Segoe UI", 13, FontStyle.Bold);
            using var fS     = new Font("Segoe UI", 8);
            using var fH     = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            using var fC     = new Font("Segoe UI", 7.5f);
            using var fTotal = new Font("Segoe UI", 11, FontStyle.Bold);
            using var br     = new SolidBrush(Color.Black);
            using var brG    = new SolidBrush(Color.Gray);
            using var pen    = new Pen(Color.FromArgb(180, 185, 200));

            if (ps == 0)
            {
                if (!string.IsNullOrWhiteSpace(firma))
                {
                    using var ff = new Font("Segoe UI", 9, FontStyle.Bold);
                    g.DrawString(firma, ff, br, lm, y); y += 18;
                }
                g.DrawString(titleStr, fT, br, lm, y); y += 24;
                g.DrawString(L("report_date", reportDateInfo), fS, brG, lm, y); y += 16;
                g.DrawLine(pen, lm, y, lm + pw, y); y += 6;
            }

            float[] weights = { 100, 300, 80, 200, 150 };
            float totalWeight = weights.Sum();
            float[] w = weights.Select(wt => (wt / totalWeight) * pw).ToArray();
            
            string[] hdr = { L("date"), L("stock_name"), L("quantity"), L("delivered_to"), L("category") };

            using var brHd = new SolidBrush(Color.FromArgb(230, 235, 245));
            g.FillRectangle(brHd, lm, y, pw, 16);
            float x = lm;
            for (int i = 0; i < hdr.Length; i++) { g.DrawString(hdr[i], fH, br, x + 2, y + 2); x += w[i]; }
            y += 18;

            int endRow = Math.Min(ps + rpp, grid.Rows.Count);
            using var brAlt = new SolidBrush(Color.FromArgb(245, 247, 252));
            for (int i = ps; i < endRow; i++)
            {
                var row = grid.Rows[i];
                if (i % 2 == 0) g.FillRectangle(brAlt, lm, y, pw, 15);
                x = lm;
                string[] cells =
                {
                    Convert.ToDateTime(row.Cells[0].Value).ToString("dd.MM.yyyy HH:mm"),
                    row.Cells[1].Value?.ToString() ?? "",
                    row.Cells[2].Value?.ToString() ?? "",
                    row.Cells[3].Value?.ToString() ?? "",
                    row.Cells[4].Value?.ToString() ?? ""
                };
                using var sfTrim = new StringFormat
                {
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                for (int ci = 0; ci < cells.Length; ci++)
                {
                    g.DrawString(cells[ci], fC, br, new RectangleF(x + 2, y + 1, w[ci] - 4, 14), sfTrim);
                    x += w[ci];
                }
                g.DrawLine(pen, lm, y + 15, lm + pw, y + 15);
                y += 16;
            }

            if (ps + rpp >= grid.Rows.Count)
            {
                y += 12;
                g.DrawLine(pen, lm, y, lm + pw, y); y += 8;
                string totalStr = L("total_consumption") + ": " + lblTotalInfo.Text;
                using var brTotal = new SolidBrush(Color.DarkBlue);
                g.DrawString(totalStr, fTotal, brTotal, lm, y);
            }

            g.DrawString(
                L("total_records_page", grid.Rows.Count, ps / rpp + 1, tp),
                fS, brG, lm, e.MarginBounds.Bottom - 8);

            ps += rpp;
            e.HasMorePages = ps < grid.Rows.Count;
        };

        using var pv = new PrintPreviewDialog
        {
            Document = pd, Width = 1100, Height = 700,
            StartPosition = FormStartPosition.CenterParent
        };
        pv.ShowDialog(this);
    }
}
