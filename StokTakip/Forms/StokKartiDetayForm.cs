using System.Drawing.Printing;
using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class StokKartiDetayForm : Form
{
    private DataGridView grid = new();
    private Label lblAd = new(), lblKod = new(), lblKategori = new(), lblAciklama = new(), lblMevcut = new();
    private Label lblGirisOzet = new(), lblCikisOzet = new(), lblUstKart = new();
    private readonly int _stokKartId;
    private StokKarti? _kart;
    private List<StokHareketi> _hareketler = new();

    public StokKartiDetayForm(int stokKartId)
    {
        _stokKartId = stokKartId;
        Text = L("stock_card_detail"); Size = new Size(960, 680); MinimumSize = new Size(700, 500);
        StartPosition = FormStartPosition.CenterParent; BackColor = UIHelper.BgDark;

        // Header
        var pnlH = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = UIHelper.BgPanel };
        pnlH.Controls.Add(new Label { Text = L("stock_card_detail"), Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = Color.White, Left = 20, Top = 12, AutoSize = true });

        // Bilgi
        var pnlInfo = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = Color.FromArgb(18, 22, 30), Padding = new Padding(20, 10, 20, 10) };
        lblAd = new Label { Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.White, Left = 20, Top = 8, AutoSize = true };
        lblKod = new Label { Font = new Font("Segoe UI", 10), ForeColor = UIHelper.AccentBlue, Left = 20, Top = 36, AutoSize = true };
        lblKategori = new Label { Font = new Font("Segoe UI", 9), ForeColor = UIHelper.AccentCyan, Left = 20, Top = 58, AutoSize = true };
        lblUstKart = new Label { Font = new Font("Segoe UI", 9), ForeColor = UIHelper.TextSecondary, Left = 20, Top = 76, AutoSize = true };
        lblAciklama = new Label { Font = new Font("Segoe UI", 9), ForeColor = UIHelper.TextSecondary, Left = 20, Top = 96, Width = 500, AutoSize = true };

        var pnlStok = new Panel { Width = 180, Height = 80, BackColor = Color.FromArgb(25, 30, 42), Left = 720, Top = 8, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        pnlStok.Controls.Add(new Label { Text = L("current_stock_label"), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = UIHelper.TextMuted, Left = 10, Top = 6, AutoSize = true });
        lblMevcut = new Label { Font = new Font("Segoe UI", 24, FontStyle.Bold), Left = 10, Top = 26, AutoSize = true };
        pnlStok.Controls.Add(lblMevcut);

        var pnlOzet = new Panel { Width = 180, Height = 36, BackColor = Color.FromArgb(25, 30, 42), Left = 720, Top = 92, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        lblGirisOzet = new Label { Font = new Font("Segoe UI", 8), ForeColor = UIHelper.AccentGreen, Left = 10, Top = 2, AutoSize = true };
        lblCikisOzet = new Label { Font = new Font("Segoe UI", 8), ForeColor = UIHelper.StokWarning, Left = 10, Top = 18, AutoSize = true };
        pnlOzet.Controls.AddRange(new Control[] { lblGirisOzet, lblCikisOzet });

        pnlInfo.Controls.AddRange(new Control[] { lblAd, lblKod, lblKategori, lblUstKart, lblAciklama, pnlStok, pnlOzet });

        // Hareket başlığı
        var pnlMH = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = UIHelper.BgDark };
        var mhTitle = UIHelper.MakeIconTitle(
            L("movement_history"),
            UIHelper.TextPrimary,
            new Font("Segoe UI", 10, FontStyle.Bold),
            left: 20, top: 6, gap: 6, iconSize: 12f, iconTop: 1, textTop: 0);
        pnlMH.Controls.Add(mhTitle);

        // Grid
        grid = new DataGridView { Dock = DockStyle.Fill }; UIHelper.StyleGrid(grid);
        grid.Columns.Add("Id", "Id"); grid.Columns["Id"]!.Visible = false;
        grid.Columns.Add("Tarih", L("date")); grid.Columns["Tarih"]!.FillWeight = 80;
        grid.Columns.Add("GC", L("entry") + "/" + L("exit")); grid.Columns["GC"]!.FillWeight = 55;
        grid.Columns.Add("TeslimEdilen", L("delivered_to")); grid.Columns["TeslimEdilen"]!.FillWeight = 120;
        grid.Columns.Add("Dept", L("department")); grid.Columns["Dept"]!.FillWeight = 100;
        grid.Columns.Add("Aciklama", L("description")); grid.Columns["Aciklama"]!.FillWeight = 200;
        grid.CellFormatting += (_, e) => {
            if (e.RowIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "GC") {
                string v = e.Value?.ToString() ?? "";
                if (e.CellStyle != null) {
                    e.CellStyle.ForeColor = v.Contains("[Ç]") ? UIHelper.StokWarning : UIHelper.AccentGreen;
                    e.CellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                }
            }
        };

        // Toolbar
        var pnlBar = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = UIHelper.BgPanel };
        var btnAdd = UIHelper.MakeButton(L("add_movement"), UIHelper.AccentGreen, 12, 8, 140);
        var btnPrint = UIHelper.MakeButton(L("print"), UIHelper.BtnMid, 160, 8, 100);
        var btnClose = UIHelper.MakeButton(L("close"), UIHelper.BtnDark, 820, 8); btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnAdd.Click += (_, _) => { if (_kart != null) { using var f = new HareketEkleForm(_kart); if (f.ShowDialog() == DialogResult.OK) YukleVeri(); } };
        btnPrint.Click += (_, _) => Yazdir(); btnClose.Click += (_, _) => Close();
        pnlBar.Controls.AddRange(new Control[] { btnAdd, btnPrint, btnClose });

        Controls.Add(grid); Controls.Add(pnlMH); Controls.Add(pnlInfo); Controls.Add(pnlH); Controls.Add(pnlBar);
        YukleVeri();
    }

    private void YukleVeri()
    {
        _kart = Program.DB!.StokKartiDetayGetir(_stokKartId);
        if (_kart == null) { MessageBox.Show(L("card_not_found")); Close(); return; }

        Text = L("stock_card_detail") + " – " + _kart.Ad;
        lblAd.Text = _kart.Ad;
        lblKod.Text = L("code_no") + ": " + _kart.KodNo;
        lblKategori.Text = string.IsNullOrWhiteSpace(_kart.Kategori) ? "" : L("category") + ": " + _kart.Kategori;
        lblUstKart.Text = string.IsNullOrWhiteSpace(_kart.UstKartAd) ? "" : L("parent_card_col") + ": " + _kart.UstKartAd;
        lblAciklama.Text = _kart.Aciklama;
        lblMevcut.Text = UIHelper.FormatMiktar(_kart.MevcutStok);
        lblMevcut.ForeColor = UIHelper.StokRengi(_kart.MevcutStok);

        _hareketler = Program.DB!.HareketleriGetir(_stokKartId);
        var son30 = _hareketler.Where(h => h.Tarih >= DateTime.Now.AddDays(-30)).ToList();
        lblGirisOzet.Text = $"▲ {L("total_entry")}: {UIHelper.FormatMiktar(son30.Where(h => h.Tur == "Giris").Sum(h => h.Miktar))}";
        lblCikisOzet.Text = $"▼ {L("total_exit")}: {UIHelper.FormatMiktar(son30.Where(h => h.Tur == "Cikis").Sum(h => h.Miktar))}";

        grid.Rows.Clear();
        foreach (var h in _hareketler)
        {
            string gc = h.Tur == "Giris" ? $"{UIHelper.FormatMiktar(h.Miktar)}[G]" : $"{UIHelper.FormatMiktar(h.Miktar)}[Ç]";
            grid.Rows.Add(h.Id, h.Tarih.ToString("dd.MM.yyyy HH:mm"), gc, h.TeslimEdilen, h.Departman, h.Aciklama);
        }
    }

    private void Yazdir()
    {
        if (_kart == null) return;
        string firma = Program.Settings.CompanyName;
        var pd = new PrintDocument();
        pd.DefaultPageSettings.Landscape = true;
        pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 1169, 827);
        pd.DefaultPageSettings.Margins = new Margins(40, 40, 50, 50);
        int ps = 0; const int rpp = 28; int tp = Math.Max(1, (int)Math.Ceiling((double)_hareketler.Count / rpp));

        pd.PrintPage += (_, e) =>
        {
            var g = e.Graphics!; float y = e.MarginBounds.Top, lm = e.MarginBounds.Left, pw = e.MarginBounds.Width;
            using var fT = new Font("Segoe UI", 13, FontStyle.Bold); using var fS = new Font("Segoe UI", 8);
            using var fH = new Font("Segoe UI", 7.5f, FontStyle.Bold); using var fC = new Font("Segoe UI", 7.5f);
            using var br = new SolidBrush(Color.Black); using var brG = new SolidBrush(Color.Gray);
            using var pen = new Pen(Color.FromArgb(180, 185, 200));

            if (ps == 0)
            {
                if (!string.IsNullOrWhiteSpace(firma)) { g.DrawString(firma, fH, br, lm, y); y += 16; }
                g.DrawString(L("detail_report"), fT, br, lm, y); y += 24;
                g.DrawString($"{L("stock")}: {_kart.Ad} ({_kart.KodNo})", fS, br, lm, y); y += 14;
                if (!string.IsNullOrWhiteSpace(_kart.Kategori)) { g.DrawString($"{L("category")}: {_kart.Kategori}", fS, brG, lm, y); y += 14; }
                g.DrawString($"{L("current_stock")}: {UIHelper.FormatMiktar(_kart.MevcutStok)} Adet", fS, br, lm, y); y += 14;
                g.DrawString(L("report_date", DateTime.Now.ToString("dd.MM.yyyy HH:mm")), fS, brG, lm, y); y += 14;
                g.DrawLine(pen, lm, y, lm + pw, y); y += 6;
            }

            float[] w = { 70, 170, 150, 70, 120, 95, 0 }; float u = 0; foreach (var ww in w) u += ww; w[^1] = pw - u;
            string[] hdr = { L("code_no"), L("stock_name"), L("delivered_to"), L("entry") + "/" + L("exit"), L("department"), L("date"), L("description") };
            using var brHd = new SolidBrush(Color.FromArgb(230, 235, 245));
            g.FillRectangle(brHd, lm, y, pw, 16); float x = lm;
            for (int i = 0; i < hdr.Length; i++) { g.DrawString(hdr[i], fH, br, x + 2, y + 2); x += w[i]; } y += 18;

            int end = Math.Min(ps + rpp, _hareketler.Count);
            using var brAlt = new SolidBrush(Color.FromArgb(245, 247, 252));
            for (int i = ps; i < end; i++)
            {
                var h = _hareketler[i]; if (i % 2 == 0) g.FillRectangle(brAlt, lm, y, pw, 15);
                x = lm; bool giris = h.Tur == "Giris";
                string gc = giris ? $"{UIHelper.FormatMiktar(h.Miktar)}[G]" : $"{UIHelper.FormatMiktar(h.Miktar)}[Ç]";
                string[] cells = { h.StokKartKodNo, h.StokKartAd, h.TeslimEdilen, gc, h.Departman, h.Tarih.ToString("dd.MM.yyyy HH:mm"), h.Aciklama };
                for (int c = 0; c < cells.Length; c++)
                {
                    Color clr = c == 3 ? (giris ? Color.DarkGreen : Color.DarkRed) : Color.Black;
                    using var brC = new SolidBrush(clr);
                    g.DrawString(cells[c], c == 3 ? fH : fC, brC, new RectangleF(x + 2, y + 1, w[c] - 4, 14), new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });
                    x += w[c];
                }
                g.DrawLine(pen, lm, y + 15, lm + pw, y + 15); y += 16;
            }
            g.DrawString(L("total_movements_page", _hareketler.Count, ps / rpp + 1, tp), fS, brG, lm, e.MarginBounds.Bottom - 8);
            ps += rpp; e.HasMorePages = ps < _hareketler.Count;
        };
        using var pv = new PrintPreviewDialog { Document = pd, Width = 1100, Height = 700, StartPosition = FormStartPosition.CenterParent }; pv.ShowDialog(this);
    }
}
