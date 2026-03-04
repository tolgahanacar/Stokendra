using StokTakip.Models;
using static StokTakip.LocalizationManager;

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
            new Label { Text = L("dashboard"), Font = UIHelper.FontTitle, ForeColor = UIHelper.TextWhite, Left = 28, Top = 6, AutoSize = true },
            new Label { Text = L("dashboard_subtitle"), Font = new Font("Segoe UI Semibold", 10), ForeColor = UIHelper.TextSecondary, Left = 28, Top = 38, AutoSize = true }
        });

        // ═══ STAT CARDS — responsive TableLayoutPanel ═══
        var tblCards = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 108, ColumnCount = 6, RowCount = 1,
            BackColor = UIHelper.BgDark, Padding = new Padding(20, 4, 20, 4)
        };
        for (int i = 0; i < 6; i++) tblCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66f));

        var cards = new (string title, string value, string sub, Color accent)[] {
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
            var card = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.BgCard, Margin = new Padding(4, 2, 4, 2) };
            var aLine = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = c.accent };
            var lblT = new Label { Text = c.title.ToUpperInvariant(), Left = 12, Top = 8, AutoSize = true, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = UIHelper.TextMuted };
            var lblV = new Label { Text = c.value, Left = 12, Top = 28, AutoSize = true, Font = new Font("Segoe UI", 20, FontStyle.Bold), ForeColor = c.accent };
            var lblS = new Label { Text = c.sub, Left = 12, Top = 72, AutoSize = true, Font = new Font("Segoe UI Semibold", 7.5f), ForeColor = UIHelper.TextDim };
            card.Controls.AddRange(new Control[] { aLine, lblT, lblV, lblS });
            card.MouseEnter += (_, _) => card.BackColor = UIHelper.BgHover; card.MouseLeave += (_, _) => card.BackColor = UIHelper.BgCard;
            foreach (Control cc in card.Controls) { cc.MouseEnter += (_, _) => card.BackColor = UIHelper.BgHover; cc.MouseLeave += (_, _) => card.BackColor = UIHelper.BgCard; }
            tblCards.Controls.Add(card, i, 0);
        }

        // ═══ SPLIT — SplitContainer for low stock + recent ═══
        var pnlBody = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.BgDark, Padding = new Padding(24, 4, 24, 10) };

        var lblLow = new Label { Text = L("low_stock_alerts"), Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = UIHelper.StokWarning, Dock = DockStyle.Top, Height = 28, Padding = new Padding(2, 4, 0, 0) };
        var gridLow = new DataGridView { Dock = DockStyle.Top, Height = 160 }; UIHelper.StyleGrid(gridLow);
        gridLow.Columns.Add("KodNo", L("code_no")); gridLow.Columns["KodNo"]!.FillWeight = 55;
        gridLow.Columns.Add("Ad", L("stock_name")); gridLow.Columns["Ad"]!.FillWeight = 160;
        gridLow.Columns.Add("Kategori", L("category")); gridLow.Columns["Kategori"]!.FillWeight = 90;
        gridLow.Columns.Add("MevcutStok", L("current_stock")); gridLow.Columns["MevcutStok"]!.FillWeight = 55;
        gridLow.Columns.Add("MinStok", L("min_stock")); gridLow.Columns["MinStok"]!.FillWeight = 45;
        gridLow.CellFormatting += (_, e) => { if (e.RowIndex >= 0 && gridLow.Columns[e.ColumnIndex].Name == "MevcutStok" && double.TryParse(e.Value?.ToString(), out double s)) { e.CellStyle.ForeColor = UIHelper.StokRengi(s); e.CellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold); } };
        foreach (var k in kartlar.Where(k => k.MevcutStok <= 3 && k.KartTipi == "Alt").OrderBy(k => k.MevcutStok).Take(10))
            gridLow.Rows.Add(k.KodNo, k.Ad, k.Kategori, UIHelper.FormatMiktar(k.MevcutStok), k.MinStok);

        var lblRec = new Label { Text = L("recent_movements"), Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = UIHelper.TextPrimary, Dock = DockStyle.Top, Height = 28, Padding = new Padding(2, 6, 0, 0) };
        var gridRec = new DataGridView { Dock = DockStyle.Fill }; UIHelper.StyleGrid(gridRec);
        gridRec.Columns.Add("Tarih", L("date")); gridRec.Columns["Tarih"]!.FillWeight = 70;
        gridRec.Columns.Add("KodNo", L("code_no")); gridRec.Columns["KodNo"]!.FillWeight = 50;
        gridRec.Columns.Add("StokAd", L("stock_name")); gridRec.Columns["StokAd"]!.FillWeight = 140;
        gridRec.Columns.Add("GC", L("entry") + "/" + L("exit")); gridRec.Columns["GC"]!.FillWeight = 48;
        gridRec.Columns.Add("Dept", L("department")); gridRec.Columns["Dept"]!.FillWeight = 75;
        gridRec.Columns.Add("Teslim", L("delivered_to")); gridRec.Columns["Teslim"]!.FillWeight = 95;
        gridRec.CellFormatting += (_, e) => { if (e.RowIndex >= 0 && gridRec.Columns[e.ColumnIndex].Name == "GC") { string v = e.Value?.ToString() ?? ""; e.CellStyle.ForeColor = v.Contains("[Ç]") ? UIHelper.StokWarning : UIHelper.AccentGreen; e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold); } };

        foreach (var h in hareketler.Take(15))
        {
            string gc = h.Tur == "Giris" ? $"{UIHelper.FormatMiktar(h.Miktar)}[G]" : $"{UIHelper.FormatMiktar(h.Miktar)}[Ç]";
            gridRec.Rows.Add(h.Tarih.ToString("dd.MM.yyyy HH:mm"), h.StokKartKodNo, h.StokKartAd, gc, h.Departman, h.TeslimEdilen);
        }

        pnlBody.Controls.Add(gridRec); pnlBody.Controls.Add(lblRec); pnlBody.Controls.Add(gridLow); pnlBody.Controls.Add(lblLow);
        Controls.Add(pnlBody); Controls.Add(tblCards); Controls.Add(pnlH);
    }
}
