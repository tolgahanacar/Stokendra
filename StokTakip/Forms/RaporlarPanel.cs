using System.Data;
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

public class RaporlarPanel : UserControl
{
    private DateTimePicker dtpStart = new(), dtpEnd = new();
    private ComboBox cmbUser = new(), cmbDept = new(), cmbCategory = new();
    private FormsPlot plotChart = new();
    private DataGridView grid = new();
    private System.Windows.Forms.Label lblTotalInfo    = new();
    private System.Windows.Forms.Label lblUniqueInfo   = new();
    private System.Windows.Forms.Label lblInfo         = new();

    public RaporlarPanel()
    {
        BackColor = UIHelper.BgDark; Dock = DockStyle.Fill; DoubleBuffered = true;

        // ═══ HEADER ═══
        var pnlH = UIHelper.MakeHeader(L("reports"), L("reports_subtitle"));

        // ═══ FILTER BAR ═══
        var pnlF = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true,
            WrapContents = true, BackColor = UIHelper.BgPanel,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(16, 8, 16, 8)
        };

        void AddFilterGroup(string label, Control ctrl)
        {
            pnlF.Controls.Add(new System.Windows.Forms.Label
            {
                Text = label, AutoSize = true,
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = UIHelper.TextMuted,
                Margin = new Padding(6, 12, 4, 0)
            });
            ctrl.Margin = new Padding(0, 6, 8, 6);
            pnlF.Controls.Add(ctrl);
        }

        dtpStart = new DateTimePicker
        {
            Width = 108, Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd.MM.yyyy",
            Value = DateTime.Today.AddDays(-30)
        };
        UIHelper.StyleDatePicker(dtpStart);

        dtpEnd = new DateTimePicker
        {
            Width = 108, Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd.MM.yyyy",
            Value = DateTime.Today
        };
        UIHelper.StyleDatePicker(dtpEnd);

        cmbUser = MakeCombo(140);
        cmbUser.Items.Add(L("all"));
        foreach (var u in Program.DB!.GetTeslimEdilenler()) cmbUser.Items.Add(u);
        cmbUser.SelectedIndex = 0;

        cmbDept = MakeCombo(120);
        cmbDept.Items.Add(L("all"));
        foreach (var d in Program.DB!.DepartmanlariGetir()) cmbDept.Items.Add(d);
        cmbDept.SelectedIndex = 0;

        cmbCategory = MakeCombo(120);
        cmbCategory.Items.Add(L("all"));
        foreach (var c in Program.DB!.AltKartlariGetir()
                     .Where(k => !string.IsNullOrEmpty(k.Kategori))
                     .Select(k => k.Kategori).Distinct().OrderBy(k => k))
            cmbCategory.Items.Add(c);
        cmbCategory.SelectedIndex = 0;

        AddFilterGroup(L("start_date"),    dtpStart);
        AddFilterGroup(L("end_date"),      dtpEnd);
        AddFilterGroup(L("select_user"),   cmbUser);
        AddFilterGroup(L("dept_filter"),   cmbDept);
        AddFilterGroup(L("select_category"), cmbCategory);

        // Divider
        pnlF.Controls.Add(new Panel
        {
            Width = 1, Height = 28,
            BackColor = UIHelper.Divider,
            Margin = new Padding(4, 10, 4, 10)
        });

        var btnGen = UIHelper.MakeFlowButton("⚡ " + L("generate_report"), UIHelper.AccentBlue, 120, 28);
        var btnClr = UIHelper.MakeFlowButton("✕ " + L("clear_filter"),    UIHelper.BtnDark,   90,  28);
        btnGen.Margin = new Padding(0, 8, 4, 8);
        btnClr.Margin = new Padding(0, 8, 0, 8);
        btnGen.Click += (_, _) => GenerateReport();
        btnClr.Click += (_, _) =>
        {
            dtpStart.Value = DateTime.Today.AddDays(-30);
            dtpEnd.Value = DateTime.Today;
            cmbUser.SelectedIndex = 0;
            cmbDept.SelectedIndex = 0;
            cmbCategory.SelectedIndex = 0;
            GenerateReport();
        };
        pnlF.Controls.AddRange(new Control[] { btnGen, btnClr });

        // ═══ ACTION TOOLBAR ═══
        var pnlT = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 40, BackColor = UIHelper.BgDark,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(16, 4, 16, 4), WrapContents = false
        };
        var btnPrint = UIHelper.MakeFlowButton("🖨 " + L("print"), UIHelper.BtnMid, 110, 28);
        btnPrint.Margin = new Padding(0, 2, 6, 2);
        btnPrint.Click += (_, _) => PrintReport();
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

        // Stat cards (right column — stacked)
        var pnlStats = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UIHelper.BgDark,
            Margin = new Padding(8, 0, 0, 0),
            Padding = new Padding(0)
        };

        var cardTotal  = MakeReportStatCard(L("total_consumption"), UIHelper.AccentPurple, 0);
        var cardUnique = MakeReportStatCard(L("unique_items"),       UIHelper.AccentCyan,   148);
        lblTotalInfo  = (System.Windows.Forms.Label)cardTotal.Controls["val"]!;
        lblUniqueInfo = (System.Windows.Forms.Label)cardUnique.Controls["val"]!;
        lblTotalInfo.Text  = "0";
        lblUniqueInfo.Text = "0";
        pnlStats.Controls.AddRange(new Control[] { cardTotal, cardUnique });

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
        var lblG = new System.Windows.Forms.Label
        {
            Text = L("movement_history"),
            Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
            ForeColor = UIHelper.TextPrimary,
            Left = 0, Top = 8, AutoSize = true
        };
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

        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "Miktar" && e.CellStyle != null)
            {
                e.CellStyle.ForeColor = UIHelper.StokWarning;
                e.CellStyle.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            }
        };

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
        Controls.Add(pnlT);
        Controls.Add(pnlTop);
        Controls.Add(pnlF);
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
            Left = 0, Top = top, Width = 300, Height = 136,
            BackColor = UIHelper.BgCard,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // Accent left bar
        card.Controls.Add(new Panel
        {
            Left = 0, Top = 0, Width = 4, Height = 136,
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

        // Resize → keep width in sync with parent
        card.ParentChanged += (_, _) =>
        {
            if (card.Parent == null) return;
            card.Width = card.Parent.ClientSize.Width;
            card.Parent.Resize += (_, _) => card.Width = card.Parent.ClientSize.Width;
        };

        return card;
    }

    // ── Report Generation ──────────────────────────────────────────────────────

    private void GenerateReport()
    {
        var start = dtpStart.Value.Date;
        var end   = dtpEnd.Value.Date.AddDays(1).AddTicks(-1);
        string? user = cmbUser.SelectedIndex    > 0 ? cmbUser.SelectedItem!.ToString()    : null;
        string? dept = cmbDept.SelectedIndex    > 0 ? cmbDept.SelectedItem!.ToString()    : null;
        string? cat  = cmbCategory.SelectedIndex > 0 ? cmbCategory.SelectedItem!.ToString() : null;

        var allMoves = Program.DB!.HareketleriGetir(null, start, end, null, "Cikis");
        var filtered = allMoves.Where(h =>
            (user == null || h.TeslimEdilen == user) &&
            (dept == null || h.Departman    == dept)
        ).ToList();

        var kartlar = Program.DB!.AltKartlariGetir();
        var query = from h in filtered
                    join k in kartlar on h.StokKartId equals k.Id
                    where (cat == null || k.Kategori == cat)
                    select new { Movement = h, Card = k };

        var rows = query.OrderBy(q => q.Movement.Tarih).ToList();

        // Grid
        grid.Rows.Clear();
        double totalConsumption = 0;
        foreach (var row in rows)
        {
            grid.Rows.Add(
                row.Movement.Tarih,
                row.Card.Ad,
                $"{UIHelper.FormatMiktar(row.Movement.Miktar)} [Ç]",
                row.Movement.TeslimEdilen,
                row.Card.Kategori);
            totalConsumption += row.Movement.Miktar;
        }

        int uniqueItems = rows.Select(r => r.Card.Id).Distinct().Count();
        lblTotalInfo.Text  = UIHelper.FormatMiktar(totalConsumption);
        lblUniqueInfo.Text = uniqueItems.ToString();
        lblInfo.Text = rows.Count == 0
            ? L("report_no_data")
            : L("records_info", rows.Count, 0);

        // Chart
        plotChart.Plot.Clear();
        if (rows.Count > 0)
        {
            var grouped = rows
                .GroupBy(r => r.Card.Ad)
                .Select(g => new { Ad = g.Key, Miktar = g.Sum(r => r.Movement.Miktar) })
                .OrderByDescending(x => x.Miktar).Take(10).ToList();

            double[] pos    = Enumerable.Range(0, grouped.Count).Select(x => (double)x).ToArray();
            double[] values = grouped.Select(x => x.Miktar).ToArray();
            string[] labels = grouped.Select(x => x.Ad).ToArray();

            var bars = plotChart.Plot.Add.Bars(pos, values);
            bars.Color      = ScottPlot.Color.FromColor(UIHelper.AccentPurple);
            bars.LegendText = L("consumption_chart");
            foreach (var b in bars.Bars) b.Size = 0.6;

            string[] shortLabels = labels
                .Select(l => l.Length > 16 ? l[..14] + ".." : l).ToArray();
            plotChart.Plot.Axes.Bottom.SetTicks(pos, shortLabels);
            plotChart.Plot.Axes.Bottom.TickLabelStyle.Rotation  = -50;
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
        }

        plotChart.Refresh();
    }

    // ── Print ──────────────────────────────────────────────────────────────────

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

            float[] w = { 100, 300, 80, 200, 0 };
            float u = 0; foreach (var ww in w) u += ww; w[^1] = pw - u;
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
                g.DrawString(totalStr, fTotal, new SolidBrush(Color.DarkBlue), lm, y);
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