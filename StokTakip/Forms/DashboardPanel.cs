using StokTakip.Models;
using static StokTakip.LocalizationManager;
using ScottPlot;
using ScottPlot.WinForms;
using System.Data;

namespace StokTakip.Forms;

public class DashboardPanel : UserControl
{
    public DashboardPanel()
    {
        BackColor = UIHelper.BgDark; Dock = DockStyle.Fill; DoubleBuffered = true;

        var kartlar = Program.DB!.StokKartlariniGetir();
        var hareketler = Program.DB!.HareketleriGetir();

        int toplamKart = kartlar.Count;
        double toplamStok = kartlar.Sum(k => Math.Max(0, k.MevcutStok));
        int dusuk = kartlar.Count(k => k.MevcutStok > 0 && k.MevcutStok <= 3);
        int tukenmis = kartlar.Count(k => k.MevcutStok <= 0);
        int toplamHareket = hareketler.Count;
        int bugunHareket = hareketler.Count(h => h.Tarih.Date == DateTime.Today);

        // Header
        var pnlH = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = UIHelper.BgDark };
        pnlH.Controls.AddRange(new Control[] {
            new System.Windows.Forms.Label { Text = L("dashboard"), Font = UIHelper.FontTitle, ForeColor = UIHelper.TextWhite, Left = 28, Top = 6, AutoSize = true },
            new System.Windows.Forms.Label { Text = L("dashboard_subtitle"), Font = new System.Drawing.Font("Segoe UI Semibold", 10), ForeColor = UIHelper.TextSecondary, Left = 28, Top = 38, AutoSize = true }
        });

        // ═══ STAT CARDS — responsive FlowLayoutPanel ═══
        var pnlCards = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, 
            AutoSize = true,
            WrapContents = true,
            BackColor = UIHelper.BgDark, 
            Padding = new Padding(16, 4, 16, 4)
        };

        var cards = new (string title, string value, string sub, System.Drawing.Color accent)[] {
            (L("total_stock_cards"), toplamKart.ToString(), L("total_stock_cards_sub"), UIHelper.AccentBlue),
            (L("total_stock_qty"), UIHelper.FormatMiktar(toplamStok), L("total_stock_qty_sub"), UIHelper.AccentCyan),
            (L("low_stock"), dusuk.ToString(), L("low_stock_sub"), UIHelper.StokLow),
            (L("depleted_stock"), tukenmis.ToString(), L("depleted_stock_sub"), UIHelper.StokWarning),
            (L("total_movements"), toplamHareket.ToString(), L("total_movements_sub"), UIHelper.AccentPurple),
            (L("today_movements"), bugunHareket.ToString(), "", UIHelper.AccentOrange),
        };
        for (int i = 0; i < cards.Length; i++)
        {
            var c = cards[i];
            var card = new Panel { Width = 180, Height = 100, BackColor = UIHelper.BgCard, Margin = new Padding(4, 2, 4, 2) };
            var aLine = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = c.accent };
            var lblT = new System.Windows.Forms.Label { Text = c.title.ToUpperInvariant(), Left = 12, Top = 8, AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 7.5f, System.Drawing.FontStyle.Bold), ForeColor = UIHelper.TextMuted };
            var lblV = new System.Windows.Forms.Label { Text = c.value, Left = 12, Top = 28, AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 20, System.Drawing.FontStyle.Bold), ForeColor = c.accent };
            var lblS = new System.Windows.Forms.Label { Text = c.sub, Left = 12, Top = 72, AutoSize = true, Font = new System.Drawing.Font("Segoe UI Semibold", 7.5f), ForeColor = UIHelper.TextDim };
            card.Controls.AddRange(new Control[] { aLine, lblT, lblV, lblS });
            card.MouseEnter += (_, _) => card.BackColor = UIHelper.BgHover; card.MouseLeave += (_, _) => card.BackColor = UIHelper.BgCard;
            foreach (Control cc in card.Controls) { cc.MouseEnter += (_, _) => card.BackColor = UIHelper.BgHover; cc.MouseLeave += (_, _) => card.BackColor = UIHelper.BgCard; }
            pnlCards.Controls.Add(card);
        }

        // ═══ CHARTS (ScottPlot 5.0) ═══
        var pnlCharts = new TableLayoutPanel { Dock = DockStyle.Top, Height = 280, ColumnCount = 2, RowCount = 1, BackColor = UIHelper.BgDark, Padding = new Padding(20, 10, 20, 10) };
        pnlCharts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
        pnlCharts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));

        var plotPie = new FormsPlot { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0), BackColor = UIHelper.BgCard };
        plotPie.Plot.FigureBackground.Color = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(20, 24, 34));
        plotPie.Plot.DataBackground.Color = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(20, 24, 34));
        plotPie.Plot.Axes.Frameless();
        plotPie.Plot.HideGrid();

        var plotBar = new FormsPlot { Dock = DockStyle.Fill, Margin = new Padding(10, 0, 0, 0), BackColor = UIHelper.BgCard };
        plotBar.Plot.FigureBackground.Color = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(20, 24, 34));
        plotBar.Plot.DataBackground.Color = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(20, 24, 34));
        plotBar.Plot.Axes.Color(ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(140, 145, 160)));
        plotBar.Plot.Grid.LineColor = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(35, 40, 50));

        // Populate Pie Chart (Categories)
        var categoryGroups = kartlar.Where(k => !string.IsNullOrEmpty(k.Kategori) && k.MevcutStok > 0)
                                    .GroupBy(k => k.Kategori)
                                    .Select(g => new { Name = g.Key, Count = g.Count() })
                                    .OrderByDescending(x => x.Count).Take(6).ToList();
        if (categoryGroups.Any())
        {
            List<PieSlice> slices = new();
            var palette = new ScottPlot.Palettes.Category10();
            for (int i = 0; i < categoryGroups.Count; i++)
            {
                var color = palette.Colors[i % palette.Colors.Length];
                slices.Add(new PieSlice { Value = categoryGroups[i].Count, FillColor = color });
                plotPie.Plot.Legend.ManualItems.Add(new ScottPlot.LegendItem { LabelText = categoryGroups[i].Name, FillColor = color });
            }
            var pie = plotPie.Plot.Add.Pie(slices);
            pie.ExplodeFraction = 0.05;
            
            plotPie.Plot.Axes.Title.Label.Text = "Kategorilere Göre Stok Çeşitliliği";
            plotPie.Plot.Axes.Title.Label.ForeColor = ScottPlot.Color.FromColor(System.Drawing.Color.White);
            
            plotPie.Plot.ShowLegend();
            plotPie.Plot.Legend.BackgroundColor = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(30, 35, 45));
            plotPie.Plot.Legend.FontColor = ScottPlot.Color.FromColor(System.Drawing.Color.LightGray);
            plotPie.Plot.Legend.OutlineColor = Colors.Transparent;
            plotPie.Refresh();
        }

        // Populate Bar Chart (Last 7 Days Entry/Exit)
        var dates = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(-6 + i)).ToList();
        double[] entries = new double[7];
        double[] exits = new double[7];
        for (int i = 0; i < 7; i++)
        {
            var d = dates[i];
            entries[i] = hareketler.Where(h => h.Tarih.Date == d && h.Tur == "Giris").Sum(h => h.Miktar);
            exits[i] = hareketler.Where(h => h.Tarih.Date == d && h.Tur == "Cikis").Sum(h => h.Miktar);
        }

        double[] positions = Enumerable.Range(0, 7).Select(x => (double)x).ToArray();
        var barEntry = plotBar.Plot.Add.Bars(positions, entries);
        barEntry.Color = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(46, 204, 113));
        barEntry.LegendText = "Girişler";
        
        var barExit = plotBar.Plot.Add.Bars(positions, exits);
        barExit.Color = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(231, 76, 60));
        barExit.LegendText = "Çıkışlar";
        
        // Offset bars to be side-by-side
        foreach (var bar in barEntry.Bars) { bar.Position -= 0.2; bar.Size = 0.35; }
        foreach (var bar in barExit.Bars) { bar.Position += 0.2; bar.Size = 0.35; }

        plotBar.Plot.Axes.Bottom.SetTicks(positions, dates.Select(d => d.ToString("dd.MM")).ToArray());
        plotBar.Plot.ShowLegend();
        plotBar.Plot.Axes.Title.Label.Text = "Son 7 Günlük Stok Hareketleri";
        plotBar.Plot.Axes.Title.Label.ForeColor = ScottPlot.Color.FromColor(System.Drawing.Color.White);
        plotBar.Plot.Legend.BackgroundColor = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(30, 35, 45));
        plotBar.Plot.Legend.FontColor = ScottPlot.Color.FromColor(System.Drawing.Color.LightGray);
        plotBar.Plot.Legend.OutlineColor = Colors.Transparent;
        plotBar.Refresh();

        pnlCharts.Controls.Add(plotPie, 0, 0);
        pnlCharts.Controls.Add(plotBar, 1, 0);

        // ═══ SPLIT — SplitContainer for low stock + recent ═══
        var pnlBody = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.BgDark, Padding = new Padding(24, 4, 24, 10) };
        
        var splitVertical = new SplitContainer 
        { 
            Dock = DockStyle.Fill, 
            Orientation = System.Windows.Forms.Orientation.Horizontal, 
            SplitterDistance = 180, // Allow dynamic dragging
            BackColor = UIHelper.BgDark,
            SplitterWidth = 8
        };

        // Low Stock Panel (Top)
        var pnlLow = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.BgDark };
        var lblLow = new System.Windows.Forms.Label { Text = L("low_stock_alerts"), Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold), ForeColor = UIHelper.StokWarning, Dock = DockStyle.Top, Height = 28, Padding = new Padding(2, 4, 0, 0) };
        var gridLow = new DataGridView { Dock = DockStyle.Fill }; UIHelper.StyleGrid(gridLow);
        gridLow.Columns.Add("KodNo", L("code_no")); gridLow.Columns["KodNo"]!.FillWeight = 55;
        gridLow.Columns.Add("Ad", L("stock_name")); gridLow.Columns["Ad"]!.FillWeight = 160;
        gridLow.Columns.Add("Kategori", L("category")); gridLow.Columns["Kategori"]!.FillWeight = 90;
        gridLow.Columns.Add("MevcutStok", L("current_stock")); gridLow.Columns["MevcutStok"]!.FillWeight = 55;
        gridLow.Columns.Add("MinStok", L("min_stock")); gridLow.Columns["MinStok"]!.FillWeight = 45;
        gridLow.CellFormatting += (_, e) => {
            if (e.RowIndex >= 0 && gridLow.Columns[e.ColumnIndex].Name == "MevcutStok" && double.TryParse(e.Value?.ToString(), out double s)) {
                if (e.CellStyle != null) {
                    e.CellStyle.ForeColor = UIHelper.StokRengi(s);
                    e.CellStyle.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
                }
            }
        };
        foreach (var k in kartlar.Where(k => k.MevcutStok <= 3 && k.KartTipi == "Alt").OrderBy(k => k.MevcutStok).Take(10))
            gridLow.Rows.Add(k.KodNo, k.Ad, k.Kategori, UIHelper.FormatMiktar(k.MevcutStok), k.MinStok);

        pnlLow.Controls.Add(gridLow); pnlLow.Controls.Add(lblLow);

        // Recent Movements Panel (Bottom)
        var pnlRec = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.BgDark };
        var lblRec = new System.Windows.Forms.Label { Text = L("recent_movements"), Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold), ForeColor = UIHelper.TextPrimary, Dock = DockStyle.Top, Height = 28, Padding = new Padding(2, 6, 0, 0) };
        var gridRec = new DataGridView { Dock = DockStyle.Fill }; UIHelper.StyleGrid(gridRec);
        gridRec.Columns.Add("Tarih", L("date")); gridRec.Columns["Tarih"]!.FillWeight = 70;
        gridRec.Columns.Add("KodNo", L("code_no")); gridRec.Columns["KodNo"]!.FillWeight = 50;
        gridRec.Columns.Add("StokAd", L("stock_name")); gridRec.Columns["StokAd"]!.FillWeight = 140;
        gridRec.Columns.Add("GC", L("entry") + "/" + L("exit")); gridRec.Columns["GC"]!.FillWeight = 48;
        gridRec.Columns.Add("Dept", L("department")); gridRec.Columns["Dept"]!.FillWeight = 75;
        gridRec.Columns.Add("Teslim", L("delivered_to")); gridRec.Columns["Teslim"]!.FillWeight = 95;
        gridRec.CellFormatting += (_, e) => {
            if (e.RowIndex >= 0 && gridRec.Columns[e.ColumnIndex].Name == "GC") {
                string v = e.Value?.ToString() ?? "";
                if (e.CellStyle != null) {
                    e.CellStyle.ForeColor = v.Contains("[Ç]") ? UIHelper.StokWarning : UIHelper.AccentGreen;
                    e.CellStyle.Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold);
                }
            }
        };

        foreach (var h in hareketler.Take(15))
        {
            string gc = h.Tur == "Giris" ? $"{UIHelper.FormatMiktar(h.Miktar)}[G]" : $"{UIHelper.FormatMiktar(h.Miktar)}[Ç]";
            gridRec.Rows.Add(h.Tarih.ToString("dd.MM.yyyy HH:mm"), h.StokKartKodNo, h.StokKartAd, gc, h.Departman, h.TeslimEdilen);
        }

        pnlRec.Controls.Add(gridRec); pnlRec.Controls.Add(lblRec);

        splitVertical.Panel1.Controls.Add(pnlLow);
        splitVertical.Panel2.Controls.Add(pnlRec);
        
        pnlBody.Controls.Add(splitVertical);
        Controls.Add(pnlBody); Controls.Add(pnlCharts); Controls.Add(pnlCards); Controls.Add(pnlH);
    }
}
