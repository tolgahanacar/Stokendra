using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class TopluHareketForm : Form
{
    private DataGridView grid = new();
    private ComboBox cmbTur = new(), cmbDept = new();
    private DateTimePicker dtpTarih = new();
    private TextBox txtTeslim = new();
    private List<StokKarti> _kartlar = new();

    public TopluHareketForm()
    {
        Text = L("bulk_movement"); Size = new Size(800, 560); MinimumSize = new Size(650, 400);
        StartPosition = FormStartPosition.CenterParent; BackColor = UIHelper.BgDark;

        // Header
        var pnlH = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = UIHelper.BgPanel };
        var bulkTitle = UIHelper.MakeIconTitle(
            L("bulk_movement"),
            UIHelper.TextWhite,
            new Font("Segoe UI", 13, FontStyle.Bold),
            left: 20, top: 10, gap: 6, iconSize: 13f, iconTop: 1, textTop: 0);
        pnlH.Controls.AddRange(new Control[] {
            bulkTitle,
            new Label { Text = L("bulk_movement_desc"), Font = new Font("Segoe UI", 8), ForeColor = UIHelper.TextMuted, Left = 340, Top = 16, AutoSize = true }
        });

        // Settings bar
        var pnlS = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.FromArgb(17, 20, 28) };
        int fx = 12;
        pnlS.Controls.Add(new Label { Text = L("type_filter"), ForeColor = UIHelper.TextSecondary, Left = fx, Top = 14, AutoSize = true, Font = new Font("Segoe UI", 8.5f) }); fx += 30;
        cmbTur = new ComboBox { Left = fx, Top = 10, Width = 90, DropDownStyle = ComboBoxStyle.DropDownList }; UIHelper.StyleComboBox(cmbTur);
        cmbTur.Items.AddRange(new object[] { L("entry"), L("exit") }); cmbTur.SelectedIndex = 0; fx += 100;

        pnlS.Controls.Add(new Label { Text = L("dept_filter"), ForeColor = UIHelper.TextSecondary, Left = fx, Top = 14, AutoSize = true, Font = new Font("Segoe UI", 8.5f) }); fx += 65;
        cmbDept = new ComboBox { Left = fx, Top = 10, Width = 130, DropDownStyle = ComboBoxStyle.DropDownList }; UIHelper.StyleComboBox(cmbDept);
        foreach (var d in Program.DB!.DepartmanlariGetir()) cmbDept.Items.Add(d);
        if (cmbDept.Items.Count > 0) cmbDept.SelectedIndex = 0; fx += 140;

        pnlS.Controls.Add(new Label { Text = L("delivered_to_label") + ":", ForeColor = UIHelper.TextSecondary, Left = fx, Top = 14, AutoSize = true, Font = new Font("Segoe UI", 8.5f) }); fx += 115;
        txtTeslim = new TextBox { Left = fx, Top = 10, Width = 100 }; UIHelper.StyleTextBox(txtTeslim); fx += 110;

        pnlS.Controls.Add(new Label { Text = L("date_label") + ":", ForeColor = UIHelper.TextSecondary, Left = fx, Top = 14, AutoSize = true, Font = new Font("Segoe UI", 8.5f) }); fx += 40;
        dtpTarih = new DateTimePicker { Left = fx, Top = 10, Width = 130, Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy HH:mm", Value = DateTime.Now };

        pnlS.Controls.AddRange(new Control[] { cmbTur, cmbDept, txtTeslim, dtpTarih });

        // Grid
        grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false };
        grid.ReadOnly = false; grid.MultiSelect = false; grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.BackgroundColor = UIHelper.BgDark; grid.BorderStyle = BorderStyle.None; grid.RowHeadersVisible = false;
        grid.GridColor = UIHelper.GridLine; grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = UIHelper.BgDark, ForeColor = UIHelper.TextPrimary, SelectionBackColor = UIHelper.SelectionBg, SelectionForeColor = UIHelper.TextWhite, Font = new Font("Segoe UI", 9.5f), Padding = new Padding(6, 3, 6, 3) };
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = UIHelper.BgPanel, ForeColor = UIHelper.TextMuted, Font = new Font("Segoe UI", 8.25f, FontStyle.Bold) };
        grid.ColumnHeadersHeight = 36; grid.RowTemplate.Height = 36; grid.EnableHeadersVisualStyles = false;
        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = UIHelper.BgAltRow };

        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Sec", HeaderText = "✓", Width = 40, FillWeight = 25 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "KodNo", HeaderText = L("code_no"), FillWeight = 60, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ad", HeaderText = L("stock_name"), FillWeight = 180, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mevcut", HeaderText = L("current_stock"), FillWeight = 60, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Miktar", HeaderText = L("quantity"), FillWeight = 60 });

        _kartlar = Program.DB!.AltKartlariGetir();
        foreach (var k in _kartlar)
            grid.Rows.Add(false, k.Id, k.KodNo, k.Ad, UIHelper.FormatMiktar(k.MevcutStok), "");

        grid.CellFormatting += (_, e) => {
            if (e.RowIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "Mevcut" && double.TryParse(e.Value?.ToString(), out double s)) {
                if (e.CellStyle != null) {
                    e.CellStyle.ForeColor = UIHelper.StokRengi(s);
                    e.CellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                }
            }
        };

        // Toolbar
        var pnlBar = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = UIHelper.BgPanel };
        var btnSA = UIHelper.MakeButton(L("select_all"), UIHelper.BtnMid, 12, 8, 110, 30);
        var btnDA = UIHelper.MakeButton(L("deselect_all"), UIHelper.BtnDark, 130, 8, 120, 30);
        var btnK = UIHelper.MakeButton(L("save"), UIHelper.AccentGreen, 560, 8, 100, 30); btnK.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        var btnI = UIHelper.MakeButton(L("cancel"), UIHelper.BtnDark, 668, 8, 100, 30); btnI.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnSA.Click += (_, _) => { foreach (DataGridViewRow r in grid.Rows) r.Cells["Sec"].Value = true; };
        btnDA.Click += (_, _) => { foreach (DataGridViewRow r in grid.Rows) r.Cells["Sec"].Value = false; };
        btnK.Click += Kaydet; btnI.Click += (_, _) => DialogResult = DialogResult.Cancel;
        pnlBar.Controls.AddRange(new Control[] { btnSA, btnDA, btnK, btnI });

        Controls.Add(grid); Controls.Add(pnlS); Controls.Add(pnlH); Controls.Add(pnlBar);
    }

    private void Kaydet(object? s, EventArgs e)
    {
        if (cmbDept.SelectedIndex < 0) { MessageBox.Show(L("select_dept")); return; }
        string turDb = cmbTur.SelectedIndex == 0 ? "Giris" : "Cikis";
        string dept = cmbDept.SelectedItem!.ToString()!;
        int saved = 0; var errors = new List<string>();
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.Cells["Sec"].Value is not true) continue;
            var ms = row.Cells["Miktar"].Value?.ToString()?.Trim();
            if (string.IsNullOrEmpty(ms)) continue;
            if (!double.TryParse(ms.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double m) || m <= 0)
            { if (!double.TryParse(ms, out m) || m <= 0) { errors.Add(L("invalid_qty_row", row.Index + 1, ms)); continue; } }
            Program.DB!.HareketEkle(new StokHareketi { StokKartId = Convert.ToInt32(row.Cells["Id"].Value), Tur = turDb, Miktar = m, TeslimEdilen = txtTeslim.Text.Trim(), Departman = dept, Tarih = dtpTarih.Value });
            saved++;
        }
        if (errors.Count > 0) MessageBox.Show(L("bulk_save_result", saved, string.Join("\n", errors)), L("bulk_movement"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        else if (saved == 0) MessageBox.Show(L("bulk_no_save"), L("info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        else { MessageBox.Show(L("bulk_save_success", saved), L("bulk_movement"), MessageBoxButtons.OK, MessageBoxIcon.Information); DialogResult = DialogResult.OK; }
    }
}
