using StokTakip.Models;
using static StokTakip.LocalizationManager;
using ScottPlot;
using ScottPlot.WinForms;
using System.Data;
using System.Drawing.Drawing2D;

namespace StokTakip.Forms;

/// <summary>Fully custom-painted stat card — no child controls, clean rendering</summary>
internal class StatCard : Control
{
    public string Icon     { get; set; } = "";
    public string Title    { get; set; } = "";
    public string Value    { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public System.Drawing.Color Accent { get; set; } = UIHelper.AccentBlue;

    private bool _hovered;

    public StatCard()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        // FIX 1 ► Kart boyutunu küçülttük: 6 kart tek satıra rahatça sığar
        Size = new Size(162, 96);
        Cursor = Cursors.Default;
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = GlowCard.RoundedRect(rect, 8);

        // Background gradient
        var c1 = _hovered ? UIHelper.BlendColor(UIHelper.BgCard, Accent, 0.08f) : UIHelper.BgCard;
        var c2 = _hovered ? UIHelper.BlendColor(System.Drawing.Color.FromArgb(20, 24, 34), Accent, 0.04f)
                          : System.Drawing.Color.FromArgb(20, 24, 34);
        using var bg = new LinearGradientBrush(rect, c1, c2, 90f);
        g.FillPath(bg, path);

        // Left accent bar
        using var barBrush = new SolidBrush(_hovered ? Accent : System.Drawing.Color.FromArgb(200, Accent));
        g.FillRectangle(barBrush, 0, 6, 3, Height - 12);

        // Border
        using var borderPen = new Pen(System.Drawing.Color.FromArgb(_hovered ? 45 : 20, Accent));
        g.DrawPath(borderPen, path);

        // FIX 2 ► İkon sağ üstte çiziliyor
        if (!string.IsNullOrEmpty(Icon))
        {
            using var fIcon = UIIcons.GetFont(12f);
            using var iconBrush = new SolidBrush(System.Drawing.Color.FromArgb(_hovered ? 200 : 130, Accent));
            var iconText = UIIcons.ResolveIcon(Icon);
            var iconSize = g.MeasureString(iconText, fIcon);
            g.DrawString(iconText, fIcon, iconBrush, Width - iconSize.Width - 8, 7);
        }

        // FIX 3 ► Türkçe büyük harf (ı→I, i→İ, ş→Ş vs.) — ToUpperTr extension
        using var fTitle = new System.Drawing.Font("Segoe UI", 7f, System.Drawing.FontStyle.Bold);
        using var titleBrush = new SolidBrush(UIHelper.TextMuted);
        g.DrawString(Title.ToUpperTr(), fTitle, titleBrush, 12, 9);

        // Value (large, accent)
        using var fValue = new System.Drawing.Font("Segoe UI", 22, System.Drawing.FontStyle.Bold);
        using var valueBrush = new SolidBrush(Accent);
        g.DrawString(Value, fValue, valueBrush, 9, 26);

        // Subtitle
        if (!string.IsNullOrEmpty(Subtitle))
        {
            using var fSub = new System.Drawing.Font("Segoe UI", 7.5f);
            using var subBrush = new SolidBrush(UIHelper.TextDim);
            g.DrawString(Subtitle, fSub, subBrush, 12, Height - 20);
        }
    }
}

/// <summary>Fully custom-painted low stock alert card</summary>
internal class AlertCard : Control
{
    public string StockName  { get; set; } = "";
    public string CodeInfo   { get; set; } = "";
    public string MinInfo    { get; set; } = "";
    public string StockValue { get; set; } = "0";
    public string Status     { get; set; } = "";
    public System.Drawing.Color Accent { get; set; } = UIHelper.StokWarning;

    private bool _hovered;

    public AlertCard()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(255, 78);
        Cursor = Cursors.Default;
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = GlowCard.RoundedRect(rect, 6);

        // Background
        var c1 = _hovered ? UIHelper.BlendColor(UIHelper.BgCard, Accent, 0.06f) : UIHelper.BgCard;
        var c2 = System.Drawing.Color.FromArgb(20, 24, 34);
        using var bg = new LinearGradientBrush(rect, c1, c2, 90f);
        g.FillPath(bg, path);

        // Left accent bar
        using var barBrush = new SolidBrush(System.Drawing.Color.FromArgb(_hovered ? 255 : 200, Accent));
        g.FillRectangle(barBrush, 0, 4, 3, Height - 8);

        // Border
        using var borderPen = new Pen(System.Drawing.Color.FromArgb(_hovered ? 40 : 18, Accent));
        g.DrawPath(borderPen, path);

        // Stock name (left, bold)
        using var fName = new System.Drawing.Font("Segoe UI Semibold", 9.5f);
        using var nameBrush = new SolidBrush(UIHelper.TextWhite);
        var nameRect = new RectangleF(12, 8, Width - 90, 20);
        using var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(StockName, fName, nameBrush, nameRect, sf);

        // Code + category info
        using var fCode = new System.Drawing.Font("Segoe UI", 7.5f);
        using var codeBrush = new SolidBrush(UIHelper.TextDim);
        g.DrawString(CodeInfo, fCode, codeBrush, 12, 32);

        // Min stock info
        using var fMin = new System.Drawing.Font("Segoe UI", 7.5f);
        using var minBrush = new SolidBrush(UIHelper.TextMuted);
        g.DrawString(MinInfo, fMin, minBrush, 12, 52);

        // Stock value (right, large)
        using var fVal = new System.Drawing.Font("Segoe UI", 20, System.Drawing.FontStyle.Bold);
        using var valBrush = new SolidBrush(Accent);
        var valSize = g.MeasureString(StockValue, fVal);
        g.DrawString(StockValue, fVal, valBrush, Width - valSize.Width - 12, 6);

        // Status text (right, small)
        using var fStatus = new System.Drawing.Font("Segoe UI", 7, System.Drawing.FontStyle.Bold);
        using var statusBrush = new SolidBrush(Accent);
        var statusSize = g.MeasureString(Status, fStatus);
        g.DrawString(Status, fStatus, statusBrush, Width - statusSize.Width - 12, 54);
    }
}

public class DashboardPanel : UserControl
{
    public DashboardPanel()
    {
        BackColor = UIHelper.BgDark; Dock = DockStyle.Fill; DoubleBuffered = true;
        SuspendLayout();

        var stats     = Program.DB!.DashboardIstatistikleriGetir();
        var altKartlar = Program.DB!.AltKartlariGetir();
        var hareketler = Program.DB!.HareketleriGetir();

        // ═══ HEADER ═══
        var pnlH = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = UIHelper.BgDark };
        var lblTitle = new System.Windows.Forms.Label
        {
            Text = L("dashboard"), Font = UIHelper.FontTitle, ForeColor = UIHelper.TextWhite,
            Left = 28, Top = 8, AutoSize = true
        };
        var lblSub = new System.Windows.Forms.Label
        {
            Text = L("dashboard_subtitle"),
            Font = new System.Drawing.Font("Segoe UI Semibold", 10),
            ForeColor = UIHelper.TextSecondary, Left = 28, Top = 40, AutoSize = true
        };
        var lblDate = new System.Windows.Forms.Label
        {
            Text = DateTime.Now.ToString("dd MMMM yyyy, dddd"),
            Font = new System.Drawing.Font("Segoe UI Semibold", 9.5f),
            ForeColor = UIHelper.AccentCyan, AutoSize = true
        };
        pnlH.Controls.AddRange(new Control[] { lblTitle, lblSub, lblDate });
        pnlH.Resize += (_, _) => { lblDate.Left = pnlH.Width - lblDate.Width - 32; lblDate.Top = 16; };

        // ═══ STAT CARDS ═══
        // FIX 1 ► Height = 108: kart (96) + margin (4×2) + padding (top 6 + bottom 4) = 106 — tek satır garantili
        var pnlCards = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 108,
            WrapContents = false,               // ← Sarma kapalı: taşmayı önler
            AutoScroll = false,
            BackColor = UIHelper.BgDark,
            Padding = new Padding(20, 6, 20, 4)
        };

        var cardsData = new (string icon, string title, string value, string sub, System.Drawing.Color accent)[]
        {
            ("🗂", L("total_stock_cards"),  stats.toplamKart.ToString(),              L("total_stock_cards_sub"),  UIHelper.AccentBlue),
            ("📦", L("total_stock_qty"),    UIHelper.FormatMiktar(stats.toplamStok),  L("total_stock_qty_sub"),    UIHelper.AccentCyan),
            ("⚡", L("low_stock"),          stats.dusuk.ToString(),                   L("low_stock_sub"),          UIHelper.StokLow),
            ("🔴", L("depleted_stock"),     stats.tukenmis.ToString(),                L("depleted_stock_sub"),     UIHelper.StokWarning),
            ("🔄", L("total_movements"),    stats.toplamHareket.ToString(),           L("total_movements_sub"),    UIHelper.AccentPurple),
            ("📅", L("today_movements"),    stats.bugunHareket.ToString(),            "",                          UIHelper.AccentOrange),
        };

        foreach (var c in cardsData)
        {
            pnlCards.Controls.Add(new StatCard
            {
                Icon     = c.icon,
                Title    = c.title,
                Value    = c.value,
                Subtitle = c.sub,
                Accent   = c.accent,
                Margin   = new Padding(4, 2, 4, 2)
            });
        }

        // ═══ CHARTS ═══
        var pnlCharts = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 270,
            ColumnCount = 2, RowCount = 1,
            BackColor = UIHelper.BgDark,
            Padding = new Padding(24, 4, 24, 4)
        };
        pnlCharts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
        pnlCharts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));

        var pieBorder = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.BgCard, Margin = new Padding(0, 0, 6, 0), Padding = new Padding(1) };
        var plotPie   = new FormsPlot { Dock = DockStyle.Fill, BackColor = UIHelper.BgCard };
        plotPie.Plot.FigureBackground.Color = ScottPlot.Color.FromColor(UIHelper.BgCard);
        plotPie.Plot.DataBackground.Color   = ScottPlot.Color.FromColor(UIHelper.BgCard);
        plotPie.Plot.Axes.Frameless();
        plotPie.Plot.HideGrid();

        var barBorder = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.BgCard, Margin = new Padding(6, 0, 0, 0), Padding = new Padding(1) };
        var plotBar   = new FormsPlot { Dock = DockStyle.Fill, BackColor = UIHelper.BgCard };
        plotBar.Plot.FigureBackground.Color = ScottPlot.Color.FromColor(UIHelper.BgCard);
        plotBar.Plot.DataBackground.Color   = ScottPlot.Color.FromColor(UIHelper.BgCard);
        plotBar.Plot.Axes.Color(ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(140, 145, 160)));
        plotBar.Plot.Grid.LineColor = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(35, 40, 50));

        // Pie chart data
        var catGroups = altKartlar
            .Where(k => !string.IsNullOrEmpty(k.Kategori) && k.MevcutStok > 0)
            .GroupBy(k => k.Kategori)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(6).ToList();

        if (catGroups.Any())
        {
            var slices = new List<PieSlice>();
            var palette = new ScottPlot.Palettes.Category10();
            for (int i = 0; i < catGroups.Count; i++)
            {
                var clr = palette.Colors[i % palette.Colors.Length];
                slices.Add(new PieSlice { Value = catGroups[i].Count, FillColor = clr });
                plotPie.Plot.Legend.ManualItems.Add(new ScottPlot.LegendItem { LabelText = catGroups[i].Name, FillColor = clr });
            }
            var pie = plotPie.Plot.Add.Pie(slices);
            pie.ExplodeFraction = 0.05;
            plotPie.Plot.Axes.Title.Label.Text     = L("category_stock_diversity");
            plotPie.Plot.Axes.Title.Label.ForeColor = ScottPlot.Color.FromColor(System.Drawing.Color.White);
            plotPie.Plot.ShowLegend();
            plotPie.Plot.Legend.BackgroundColor = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(30, 35, 45));
            plotPie.Plot.Legend.FontColor       = ScottPlot.Color.FromColor(System.Drawing.Color.LightGray);
            plotPie.Plot.Legend.OutlineColor    = Colors.Transparent;
            plotPie.Refresh();
        }

        // Bar chart
        var last7  = DateTime.Today.AddDays(-6);
        var recent = hareketler.Where(h => h.Tarih.Date >= last7).ToList();
        var dates  = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(-6 + i)).ToList();
        double[] ent = new double[7], ext = new double[7];
        var grp = recent.GroupBy(h => h.Tarih.Date).ToDictionary(g => g.Key, g => g);
        for (int i = 0; i < 7; i++)
        {
            if (grp.TryGetValue(dates[i], out var day))
            {
                ent[i] = day.Where(h => h.Tur == "Giris").Sum(h => h.Miktar);
                ext[i] = day.Where(h => h.Tur == "Cikis").Sum(h => h.Miktar);
            }
        }
        var pos = Enumerable.Range(0, 7).Select(x => (double)x).ToArray();
        var be  = plotBar.Plot.Add.Bars(pos, ent); be.Color = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(46, 204, 113)); be.LegendText = L("entry");
        var bx  = plotBar.Plot.Add.Bars(pos, ext); bx.Color = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(231, 76, 60));  bx.LegendText = L("exit");
        foreach (var b in be.Bars) { b.Position -= 0.2; b.Size = 0.35; }
        foreach (var b in bx.Bars) { b.Position += 0.2; b.Size = 0.35; }
        plotBar.Plot.Axes.Bottom.SetTicks(pos, dates.Select(d => d.ToString("dd.MM")).ToArray());
        plotBar.Plot.ShowLegend();
        plotBar.Plot.Axes.Title.Label.Text     = L("last_7_days_movements");
        plotBar.Plot.Axes.Title.Label.ForeColor = ScottPlot.Color.FromColor(System.Drawing.Color.White);
        plotBar.Plot.Legend.BackgroundColor = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(30, 35, 45));
        plotBar.Plot.Legend.FontColor       = ScottPlot.Color.FromColor(System.Drawing.Color.LightGray);
        plotBar.Plot.Legend.OutlineColor    = Colors.Transparent;
        plotBar.Refresh();

        pieBorder.Controls.Add(plotPie);
        barBorder.Controls.Add(plotBar);
        pnlCharts.Controls.Add(pieBorder, 0, 0);
        pnlCharts.Controls.Add(barBorder, 1, 0);

        // ═══ LOW STOCK ALERTS ═══
        var pnlBody = new SectionPanel
        {
            Dock       = DockStyle.Fill,
            Title      = UIIcons.StripLeadingIcon(L("low_stock_alerts")),
            AccentColor = UIHelper.StokWarning,
            Icon       = "🔔",
            Padding    = new Padding(8, 46, 8, 8)
        };

        var dusukKartlar = altKartlar
            .Where(k => k.MevcutStok <= 3)
            .OrderBy(k => k.MevcutStok)
            .Take(15).ToList();

        if (dusukKartlar.Count == 0)
        {
            pnlBody.Controls.Add(new System.Windows.Forms.Label
            {
                Text      = L("no_low_stock"),
                Font      = new System.Drawing.Font("Segoe UI Semibold", 11),
                ForeColor = UIHelper.AccentGreen,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
        }
        else
        {
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoScroll = true,
                WrapContents = true, BackColor = UIHelper.BgDark,
                Padding = new Padding(4, 4, 4, 4)
            };
            foreach (var k in dusukKartlar)
            {
                bool dep = k.MevcutStok <= 0;
                flow.Controls.Add(new AlertCard
                {
                    StockName  = k.Ad,
                    CodeInfo   = $"{k.KodNo}  •  {k.Kategori}",
                    MinInfo    = $"Min: {k.MinStok}",
                    StockValue = UIHelper.FormatMiktar(k.MevcutStok),
                    // FIX 3 ► Türkçe büyük harf extension
                    Status     = (dep ? L("depleted") : L("low_stock")).ToUpperTr(),
                    Accent     = dep ? UIHelper.StokWarning : UIHelper.StokLow,
                    Margin     = new Padding(4, 3, 4, 3)
                });
            }
            pnlBody.Controls.Add(flow);
        }

        Controls.Add(pnlBody);
        Controls.Add(pnlCharts);
        Controls.Add(pnlCards);
        Controls.Add(pnlH);
        ResumeLayout(true);
    }
}
