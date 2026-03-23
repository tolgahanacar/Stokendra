using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class DepartmanlarPanel : UserControl
{
    private DataGridView grid = new();
    private TextBox txtYeni = new();

    public DepartmanlarPanel()
    {
        BackColor = UIHelper.BgDark; Dock = DockStyle.Fill; DoubleBuffered = true;
        var pnlH = UIHelper.MakeHeader(L("departments"), L("departments_subtitle"));
        var pnlT = UIHelper.MakeToolbar(50);
        var lbl = new Label { Text = L("new_dept_label"), ForeColor = UIHelper.TextSecondary, Left = 12, Top = 15, AutoSize = true, Font = new Font("SF Pro Text", 9) };
        txtYeni = UIHelper.MakeSearchBox(L("dept_name_placeholder"), 250); txtYeni.Left = 140; txtYeni.Top = 11;
        txtYeni.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) Ekle(); };
        var btnE = UIHelper.MakeFlowButton(L("add_dept"), UIHelper.AccentBlue, 110);
        var btnS = UIHelper.MakeFlowButton(L("delete_selected"), UIHelper.AccentRed, 140);
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
