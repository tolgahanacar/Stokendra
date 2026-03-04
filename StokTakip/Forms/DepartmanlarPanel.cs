using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class DepartmanlarPanel : UserControl
{
    private DataGridView grid = new();
    private TextBox txtYeni = new();

    public DepartmanlarPanel()
    {
        BackColor = UIHelper.BgDark; Dock = DockStyle.Fill; DoubleBuffered = true;
        var pnlH = new Panel { Dock = DockStyle.Top, Height = 65, BackColor = UIHelper.BgDark };
        pnlH.Controls.AddRange(new Control[] {
            new Label { Text = L("departments"), Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = UIHelper.TextWhite, Left = 28, Top = 14, AutoSize = true },
            new Label { Text = L("departments_subtitle"), Font = new Font("Segoe UI", 9), ForeColor = UIHelper.TextMuted, Left = 28, Top = 44, AutoSize = true }
        });
        var pnlT = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = UIHelper.BgPanel };
        var lbl = new Label { Text = L("new_dept_label"), ForeColor = UIHelper.TextSecondary, Left = 12, Top = 15, AutoSize = true, Font = new Font("Segoe UI", 9) };
        txtYeni = new TextBox { Left = 140, Top = 11, Width = 250 }; UIHelper.StyleTextBox(txtYeni); txtYeni.PlaceholderText = L("dept_name_placeholder");
        txtYeni.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) Ekle(); };
        var btnE = UIHelper.MakeButton(L("add_dept"), UIHelper.AccentBlue, 402, 10, 90, 30);
        var btnS = UIHelper.MakeButton(L("delete_selected"), UIHelper.AccentRed, 500, 10, 110, 30);
        btnE.Click += (_, _) => Ekle(); btnS.Click += (_, _) => Sil();
        pnlT.Controls.AddRange(new Control[] { lbl, txtYeni, btnE, btnS });

        grid = new DataGridView { Dock = DockStyle.Fill }; UIHelper.StyleGrid(grid);
        grid.Columns.Add("Ad", L("dept_name_col")); grid.Columns["Ad"]!.FillWeight = 300;
        Controls.Add(grid); Controls.Add(pnlT); Controls.Add(pnlH);
        Yukle();
    }

    private void Yukle() { grid.Rows.Clear(); foreach (var d in Program.DB!.DepartmanlariGetir()) grid.Rows.Add(d); }
    private void Ekle() { if (string.IsNullOrWhiteSpace(txtYeni.Text)) return; Program.DB!.DepartmanEkle(txtYeni.Text.Trim()); txtYeni.Clear(); Yukle(); }
    private void Sil()
    {
        if (grid.SelectedRows.Count == 0) { MessageBox.Show(L("select_row_first")); return; }
        var ad = grid.SelectedRows[0].Cells["Ad"].Value?.ToString() ?? "";
        if (MessageBox.Show(L("confirm_dept_delete", ad), L("confirm_delete_title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        { Program.DB!.DepartmanSil(ad); Yukle(); }
    }
}
