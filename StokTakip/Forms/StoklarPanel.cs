using System.Drawing.Printing;
using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class StoklarPanel : UserControl
{
    private DataGridView grid = new();
    private TextBox txtAra = new();
    private Label lblInfo = new();
    private List<StokKarti> _altKartlar = new();

    public StoklarPanel()
    {
        BackColor = UIHelper.BgDark; Dock = DockStyle.Fill; DoubleBuffered = true;

        var pnlH = UIHelper.MakeHeader(L("stocks"));

        var pnlT = UIHelper.MakeToolbar(40);
        txtAra = UIHelper.MakeSearchBox(L("stock_code_search"), 300); txtAra.TextChanged += (_, _) => FilterGrid();
        
        var btnRapor = UIHelper.MakeFlowButton("Rapor Al", UIHelper.AccentBlue, 110);
        btnRapor.Click += (_, _) => RaporAl();
        
        pnlT.Controls.AddRange(new Control[] { txtAra, btnRapor });

        grid = new DataGridView { Dock = DockStyle.Fill }; UIHelper.StyleGrid(grid);
        grid.Columns.Add("Id", "Id"); grid.Columns["Id"]!.Visible = false;
        grid.Columns.Add("KodNo", L("code_no")); grid.Columns["KodNo"]!.FillWeight = 55;
        grid.Columns.Add("Ad", L("stock_name")); grid.Columns["Ad"]!.FillWeight = 170;
        grid.Columns.Add("UstKart", L("parent_card_col")); grid.Columns["UstKart"]!.FillWeight = 150;
        grid.Columns.Add("Kategori", L("category")); grid.Columns["Kategori"]!.FillWeight = 100;
        grid.Columns.Add("MevcutStok", L("current_stock")); grid.Columns["MevcutStok"]!.FillWeight = 60;
        grid.Columns.Add("MinStok", L("min_stock")); grid.Columns["MinStok"]!.FillWeight = 50;

        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (grid.Columns[e.ColumnIndex].Name == "MevcutStok" && double.TryParse(e.Value?.ToString(), out double s))
            {
                if (e.CellStyle != null)
                {
                    e.CellStyle.ForeColor = UIHelper.StokRengi(s);
                    e.CellStyle.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
                }
                
                if (s <= 3)
                    foreach (DataGridViewCell cell in grid.Rows[e.RowIndex].Cells)
                        if (cell.ColumnIndex != e.ColumnIndex && cell.Style != null)
                            cell.Style.ForeColor = s <= 0 ? UIHelper.StokWarning : UIHelper.StokLow;
            }
        };

        grid.DoubleClick += (_, _) =>
        {
            if (grid.SelectedRows.Count == 0) return;
            using var f = new StokKartiDetayForm(Convert.ToInt32(grid.SelectedRows[0].Cells["Id"].Value));
            f.ShowDialog(); YukleGrid();
        };

        // Status
        var pnlSt = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = UIHelper.BgPanel };
        lblInfo = new Label { Left = 20, Top = 8, AutoSize = true, Font = new Font("Segoe UI Semibold", 9f), ForeColor = UIHelper.TextMuted };
        pnlSt.Controls.Add(lblInfo);

        Controls.Add(grid); Controls.Add(pnlT); Controls.Add(pnlH); Controls.Add(pnlSt);
        YukleGrid();
    }

    void YukleGrid() { _altKartlar = Program.DB!.AltKartlariGetir(); FilterGrid(); }

    void FilterGrid()
    {
        grid.Rows.Clear(); var a = txtAra.Text.Trim().ToLowerInvariant(); int dusuk = 0;
        foreach (var k in _altKartlar)
        {
            if (!string.IsNullOrEmpty(a) && !k.Ad.ToLowerInvariant().Contains(a) && !k.KodNo.ToLowerInvariant().Contains(a) && !k.UstKartAd.ToLowerInvariant().Contains(a)) continue;
            grid.Rows.Add(k.Id, k.KodNo, k.Ad, string.IsNullOrEmpty(k.UstKartAd) ? "-" : k.UstKartAd, k.Kategori, UIHelper.FormatMiktar(k.MevcutStok), k.MinStok);
            if (k.MevcutStok <= 3) dusuk++;
        }
        lblInfo.Text = L("stocks_subtitle") + $"  •  {_altKartlar.Count} {L("child_card").ToLower()}  •  ⚠ {dusuk} {L("low_stock").ToLower()}";
    }

    private void RaporAl()
    {
        if (_altKartlar.Count == 0) { MessageBox.Show("Görüntülenecek stok kartı yok.", L("info")); return; }
        
        string firma = Program.Settings.CompanyName;
        var pd = new PrintDocument();
        pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169); // Portrait
        pd.DefaultPageSettings.Margins = new Margins(40, 40, 50, 50);

        // Precalculate Data
        double grandGiris = 0, grandCikis = 0, grandMevcut = 0;
        var raporVerisi = new List<(string KodNo, string Ad, double G, double C, double M)>();
        foreach(var kart in _altKartlar) 
        {
            var hareketler = Program.DB!.HareketleriGetir(kart.Id, System.DateTime.MinValue, System.DateTime.MaxValue, null, null);
            double kartGiris = hareketler.Where(h => h.Tur == "Giris").Sum(h => h.Miktar);
            double kartCikis = hareketler.Where(h => h.Tur == "Cikis").Sum(h => h.Miktar);
            
            grandGiris += kartGiris;
            grandCikis += kartCikis;
            grandMevcut += kart.MevcutStok;
            
            raporVerisi.Add((kart.KodNo, kart.Ad, kartGiris, kartCikis, kart.MevcutStok));
        }

        int ps = 0; const int rpp = 40; 
        int tp = Math.Max(1, (int)Math.Ceiling((double)raporVerisi.Count / rpp));
        
        pd.PrintPage += (_, e) => {
            var g = e.Graphics!; float y = e.MarginBounds.Top, lm = e.MarginBounds.Left, pw = e.MarginBounds.Width;
            
            using var fTitle = new Font("Segoe UI", 16, FontStyle.Bold); 
            using var fSub = new Font("Segoe UI", 9); 
            using var fHeader = new Font("Segoe UI", 9.5f, FontStyle.Bold); 
            using var fRow = new Font("Segoe UI", 9f); 
            using var fTotal = new Font("Segoe UI", 11, FontStyle.Bold); 
            using var fTotalLbl = new Font("Segoe UI", 10, FontStyle.Bold); 

            using var br = new SolidBrush(Color.Black); 
            using var brMuted = new SolidBrush(Color.FromArgb(100, 100, 100)); 
            using var brGreen = new SolidBrush(Color.DarkGreen); 
            using var brRed = new SolidBrush(Color.DarkRed);
            using var brBlue = new SolidBrush(Color.DarkBlue);
            
            using var pen = new Pen(Color.FromArgb(200, 205, 215));
            using var brHd = new SolidBrush(Color.FromArgb(230, 235, 245));
            using var brAlt = new SolidBrush(Color.FromArgb(245, 247, 252));

            // Draw Header
            if (ps == 0) { 
                if (!string.IsNullOrWhiteSpace(firma)) { 
                    using var ff = new Font("Segoe UI", 11, FontStyle.Bold); 
                    g.DrawString(firma, ff, br, lm, y); 
                    y += 24; 
                } 
                g.DrawString("STOK DURUM RAPORU", fTitle, br, lm, y); y += 30; 
                g.DrawString(L("report_date", DateTime.Now.ToString("dd.MM.yyyy HH:mm")), fSub, brMuted, lm, y); y += 20; 
                g.DrawLine(pen, lm, y, lm + pw, y); y += 15; 
            }

            // Table Headers
            float[] w = { 90, 270, 110, 110, 110 }; 
            float u = 0; foreach (var ww in w) u += ww; w[w.Length - 1] = pw - (u - w[w.Length - 1]);
            
            string[] hdr = { "Stok Kodu", "Stok Adı", "Top. Giriş", "Top. Çıkış", "Mevcut" };
            g.FillRectangle(brHd, lm, y, pw, 22);
            float x = lm;
            for (int i = 0; i < hdr.Length; i++) { 
                var sf = new StringFormat{ Alignment = i >= 2 ? StringAlignment.Far : StringAlignment.Near };
                g.DrawString(hdr[i], fHeader, br, new RectangleF(x, y + 3, w[i] - 5, 20), sf); 
                x += w[i]; 
            } 
            y += 26;

            int end = Math.Min(ps + rpp, raporVerisi.Count);
            
            for (int i = ps; i < end; i++) {
                var item = raporVerisi[i];
                if (i % 2 == 0) g.FillRectangle(brAlt, lm, y, pw, 20);
                
                x = lm;
                g.DrawString(item.KodNo, fRow, br, new RectangleF(x, y + 2, w[0], 20)); x += w[0];
                g.DrawString(item.Ad, fRow, br, new RectangleF(x, y + 2, w[1]-5, 20), new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }); x += w[1];
                
                var sfRight = new StringFormat{ Alignment = StringAlignment.Far };
                g.DrawString(UIHelper.FormatMiktar(item.G), fRow, brGreen, new RectangleF(x, y + 2, w[2]-5, 20), sfRight); x += w[2];
                g.DrawString(UIHelper.FormatMiktar(item.C), fRow, brRed, new RectangleF(x, y + 2, w[3]-5, 20), sfRight); x += w[3];
                g.DrawString(UIHelper.FormatMiktar(item.M), fRow, br, new RectangleF(x, y + 2, w[4]-5, 20), sfRight);
                
                g.DrawLine(pen, lm, y + 20, lm + pw, y + 20); 
                y += 20;
            }

            // Print Grand Totals
            if (end == raporVerisi.Count)
            {
                y += 15;
                g.FillRectangle(new SolidBrush(Color.FromArgb(235, 240, 245)), lm, y, pw, 60);
                g.DrawRectangle(new Pen(Color.FromArgb(150, 160, 180)), lm, y, pw, 60);
                
                y += 8;
                g.DrawString("GENEL TOPLAMLAR", fTotalLbl, brMuted, lm + 10, y + 10);
                
                float totalX = lm + w[0] + w[1];
                var sfRight = new StringFormat{ Alignment = StringAlignment.Far };
                
                g.DrawString("Toplam Giriş:", fSub, brMuted, new RectangleF(totalX, y - 2, w[2]-5, 20), sfRight);
                g.DrawString(UIHelper.FormatMiktar(grandGiris), fTotal, brGreen, new RectangleF(totalX, y + 15, w[2]-5, 30), sfRight);
                totalX += w[2];

                g.DrawString("Toplam Çıkış:", fSub, brMuted, new RectangleF(totalX, y - 2, w[3]-5, 20), sfRight);
                g.DrawString(UIHelper.FormatMiktar(grandCikis), fTotal, brRed, new RectangleF(totalX, y + 15, w[3]-5, 30), sfRight);
                totalX += w[3];
                
                g.DrawString("Mevcut:", fSub, brMuted, new RectangleF(totalX, y - 2, w[4]-5, 20), sfRight);
                g.DrawString(UIHelper.FormatMiktar(grandMevcut), fTotal, brBlue, new RectangleF(totalX, y + 15, w[4]-5, 30), sfRight);
            }

            g.DrawString(L("total_records_page", raporVerisi.Count, ps / rpp + 1, tp), fSub, brMuted, lm, e.MarginBounds.Bottom - 8); 
            ps += rpp; 
            e.HasMorePages = ps < raporVerisi.Count;
        };

        using var pv = new PrintPreviewDialog { Document = pd, Width = 900, Height = 1000, StartPosition = FormStartPosition.CenterParent }; 
        pv.ShowDialog();
    }
}
