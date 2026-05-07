using StokTakip.Models;
using StokTakip.Infrastructure;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public sealed class TopluHareketDuzenleForm : Form
{
    private List<StokHareketi> _hareketler;

    private CheckBox chkTeslim = new(), chkDept = new(), chkAcik = new(), chkTarih = new();

    private TextBox txtTeslim = new(), txtAciklama = new();
    private ComboBox cmbDept = new();
    private DateTimePicker dtpTarih = new();

    public TopluHareketDuzenleForm(List<StokHareketi> seciliHareketler)
    {
        _hareketler = seciliHareketler;

        Text = L("bulk_edit_title", _hareketler.Count);
        Size = new Size(460, 480);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UIHelper.BgDark;
        ForeColor = UIHelper.TextPrimary;

        var lblInfo = new Label
        {
            Text = L("bulk_edit_title", _hareketler.Count) + "\n" + L("rows_selected", _hareketler.Count),
            Dock = DockStyle.Top,
            Height = 60,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 11f),
            ForeColor = UIHelper.AccentCyan,
            BackColor = UIHelper.BgPanel
        };
        Controls.Add(lblInfo);

        var pnlM = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };

        int y = 75;

        chkTeslim = MakeCheck(L("update_delivered_to"), y);
        y += 25;
        txtTeslim = new TextBox { Left = 50, Top = y, Width = 340, Enabled = false };
        UIHelper.StyleTextBox(txtTeslim);
        chkTeslim.CheckedChanged += (_, _) => txtTeslim.Enabled = chkTeslim.Checked;
        pnlM.Controls.Add(chkTeslim);
        pnlM.Controls.Add(txtTeslim);
        y += 45;

        chkDept = MakeCheck(L("update_department"), y);
        y += 25;
        cmbDept = new ComboBox { Left = 50, Top = y, Width = 340, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
        UIHelper.StyleComboBox(cmbDept);
        chkDept.CheckedChanged += (_, _) => cmbDept.Enabled = chkDept.Checked;
        cmbDept.Items.Add("");
        foreach (var d in AppServices.Current.Departments.GetAll())
            cmbDept.Items.Add(d);
        pnlM.Controls.Add(chkDept);
        pnlM.Controls.Add(cmbDept);
        y += 45;

        chkTarih = MakeCheck(L("update_date"), y);
        y += 25;
        dtpTarih = new DateTimePicker
        {
            Left = 50,
            Top = y,
            Width = 340,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd.MM.yyyy HH:mm",
            Enabled = false
        };
        chkTarih.CheckedChanged += (_, _) => dtpTarih.Enabled = chkTarih.Checked;
        pnlM.Controls.Add(chkTarih);
        pnlM.Controls.Add(dtpTarih);
        y += 45;

        chkAcik = MakeCheck(L("update_description"), y);
        y += 25;
        txtAciklama = new TextBox { Left = 50, Top = y, Width = 340, Enabled = false };
        UIHelper.StyleTextBox(txtAciklama);
        chkAcik.CheckedChanged += (_, _) => txtAciklama.Enabled = chkAcik.Checked;
        pnlM.Controls.Add(chkAcik);
        pnlM.Controls.Add(txtAciklama);

        Controls.Add(pnlM);

        var pnlF = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = UIHelper.BgPanel };
        var btnUygula = UIHelper.MakeButton(L("apply_changes"), UIHelper.AccentBlue, 220, 15);
        var btnIptal = UIHelper.MakeButton(L("cancel"), UIHelper.BtnDark, 350, 15);
        btnUygula.Click += BtnUygula_Click;
        btnIptal.Click += (_, _) => DialogResult = DialogResult.Cancel;
        pnlF.Controls.AddRange(new Control[] { btnUygula, btnIptal });
        Controls.Add(pnlF);
    }

    private CheckBox MakeCheck(string text, int top)
    {
        return new CheckBox
        {
            Text = text,
            Left = 25,
            Top = top,
            Width = 380,
            Font = new Font("Segoe UI Semibold", 9f),
            ForeColor = UIHelper.TextPrimary,
            Cursor = Cursors.Hand
        };
    }

    private void BtnUygula_Click(object? sender, EventArgs e)
    {
        if (!chkTeslim.Checked && !chkDept.Checked && !chkAcik.Checked && !chkTarih.Checked)
        {
            DialogResult = DialogResult.Cancel;
            return;
        }

        // Değiştirilecek hareketleri hazırla
        var guncellenecekler = new List<StokHareketi>();
        foreach (var h in _hareketler)
        {
            // Orijinal nesneyi değiştirmeden kopya üzerinde çalış
            var kopya = new StokHareketi
            {
                Id = h.Id,
                StokKartId = h.StokKartId,
                Tur = h.Tur,
                Miktar = h.Miktar,
                TeslimEdilen = chkTeslim.Checked ? txtTeslim.Text.Trim() : h.TeslimEdilen,
                Departman = chkDept.Checked ? (cmbDept.SelectedItem?.ToString() ?? h.Departman) : h.Departman,
                Tarih = chkTarih.Checked ? dtpTarih.Value : h.Tarih,
                Aciklama = chkAcik.Checked ? txtAciklama.Text.Trim() : h.Aciklama
            };
            guncellenecekler.Add(kopya);
        }

        try
        {
            // Tek atomik transaction — ya hepsi ya hiçbiri
            AppServices.Current.Movements.UpdateBulk(guncellenecekler);

            MessageBox.Show(
                L("bulk_edit_success", guncellenecekler.Count),
                L("info"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                L("error_with_details", ex.Message),
                L("error"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
