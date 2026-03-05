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
        pnlT.Controls.Add(txtAra);

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
}
