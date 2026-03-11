using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class NotlarPanel : UserControl
{
    private DataGridView grid = new();
    private List<Not> _notlar = new();

    public NotlarPanel()
    {
        BackColor = UIHelper.BgDark;
        Dock = DockStyle.Fill;
        DoubleBuffered = true;

        // ── Header ──
        var pnlH = UIHelper.MakeHeader(L("notes"), L("notes_subtitle"));

        // ── Toolbar ──
        var pnlToolbar = UIHelper.MakeToolbar(44);
        var btnEkle = UIHelper.MakeFlowButton(L("new_note"), UIHelper.AccentYellow, 130);
        var btnSil = UIHelper.MakeFlowButton(L("delete"), UIHelper.AccentRed, 90);
        btnEkle.Click += (_, _) => YeniNot();
        btnSil.Click += (_, _) => SilNot();
        pnlToolbar.Controls.AddRange(new Control[] { btnEkle, btnSil });

        // ── Grid ──
        grid = new DataGridView { Dock = DockStyle.Fill };
        UIHelper.StyleGrid(grid);
        grid.Columns.Add("Id", "Id"); grid.Columns["Id"]!.Visible = false;

        var colBaslik = new DataGridViewTextBoxColumn
        {
            Name = "Baslik", HeaderText = L("note_title_placeholder"),
            FillWeight = 180
        };
        grid.Columns.Add(colBaslik);

        var colIcerik = new DataGridViewTextBoxColumn
        {
            Name = "Icerik", HeaderText = L("note_preview_col"),
            FillWeight = 300
        };
        grid.Columns.Add(colIcerik);

        var colTarih = new DataGridViewTextBoxColumn
        {
            Name = "Tarih", HeaderText = L("note_date_col"),
            FillWeight = 100
        };
        grid.Columns.Add(colTarih);

        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (grid.Columns[e.ColumnIndex].Name == "Baslik" && e.CellStyle != null)
            {
                e.CellStyle.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
                e.CellStyle.ForeColor = UIHelper.TextWhite;
            }
            if (grid.Columns[e.ColumnIndex].Name == "Tarih" && e.CellStyle != null)
            {
                e.CellStyle.ForeColor = UIHelper.AccentCyan;
                e.CellStyle.Font = new Font("Segoe UI", 8.5f);
            }
            if (grid.Columns[e.ColumnIndex].Name == "Icerik" && e.CellStyle != null)
            {
                e.CellStyle.ForeColor = UIHelper.TextMuted;
            }
        };

        grid.DoubleClick += (_, _) => DuzenleNot();

        // ── Status Bar ──
        var pnlSt = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = UIHelper.BgPanel };
        var lblInfo = new Label
        {
            Left = 20, Top = 8, AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9f),
            ForeColor = UIHelper.TextMuted, Tag = "info"
        };
        pnlSt.Controls.Add(lblInfo);

        Controls.Add(grid);
        Controls.Add(pnlToolbar);
        Controls.Add(pnlH);
        Controls.Add(pnlSt);

        YukleGrid();
    }

    private Label? InfoLabel => Controls.OfType<Panel>().SelectMany(p => p.Controls.OfType<Label>()).FirstOrDefault(l => l.Tag?.ToString() == "info");

    private void YukleGrid()
    {
        _notlar = Program.DB!.NotlariGetir();
        grid.Rows.Clear();
        foreach (var n in _notlar)
        {
            string preview = n.Icerik.Length > 80 ? n.Icerik[..80].Replace("\r\n", " ").Replace("\n", " ") + "..." : n.Icerik.Replace("\r\n", " ").Replace("\n", " ");
            grid.Rows.Add(n.Id, n.Baslik, preview, n.Tarih.ToString("dd.MM.yyyy HH:mm"));
        }
        if (InfoLabel != null)
            InfoLabel.Text = $"{_notlar.Count} {L("notes").ToLower()}";
    }

    private void YeniNot()
    {
        using var f = new NotDuzenleForm();
        if (f.ShowDialog() == DialogResult.OK)
        {
            Program.DB!.NotEkle(f.Sonuc);
            YukleGrid();
        }
    }

    private void DuzenleNot()
    {
        if (grid.SelectedRows.Count == 0) return;
        int id = Convert.ToInt32(grid.SelectedRows[0].Cells["Id"].Value);
        var not = _notlar.FirstOrDefault(n => n.Id == id);
        if (not == null) return;

        using var f = new NotDuzenleForm(not);
        if (f.ShowDialog() == DialogResult.OK)
        {
            Program.DB!.NotGuncelle(f.Sonuc);
            YukleGrid();
        }
    }

    private void SilNot()
    {
        if (grid.SelectedRows.Count == 0) { MessageBox.Show(L("select_note_first")); return; }
        int id = Convert.ToInt32(grid.SelectedRows[0].Cells["Id"].Value);
        if (MessageBox.Show(L("confirm_note_delete"), L("confirm_delete_title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            Program.DB!.NotSil(id);
            YukleGrid();
        }
    }
}
