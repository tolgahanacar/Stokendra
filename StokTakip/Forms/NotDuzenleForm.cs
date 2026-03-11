using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class NotDuzenleForm : Form
{
    private TextBox txtBaslik = new(), txtIcerik = new();
    public Not Sonuc { get; private set; } = new();

    public NotDuzenleForm(Not? mevcut = null)
    {
        Text = mevcut == null ? L("note_new_title") : L("note_edit_title");
        Size = new Size(520, 440);
        MinimumSize = new Size(400, 360);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        BackColor = UIHelper.BgDark;
        ShowInTaskbar = false;

        if (mevcut != null) Sonuc = mevcut;

        // ── Header — simple accent bar + title ──
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = UIHelper.BgPanel };
        var accentLine = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = UIHelper.AccentYellow };
        var lblFormTitle = new Label
        {
            Text = Text,
            Font = new Font("Segoe UI Semibold", 13, FontStyle.Bold),
            ForeColor = UIHelper.TextWhite,
            Left = 20, Top = 16, AutoSize = true
        };
        pnlHeader.Controls.AddRange(new Control[] { lblFormTitle, accentLine });

        // ── Body ──
        var pnlBody = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.BgDark, Padding = new Padding(24, 16, 24, 8) };

        // Tarih — simple label
        var tarihText = (mevcut?.Tarih ?? DateTime.Now).ToString("dd MMMM yyyy, HH:mm");
        var pnlTarih = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = UIHelper.BgDark };
        var tarihTitle = UIHelper.MakeIconTitle(
            "📅  " + tarihText,
            UIHelper.AccentCyan,
            new Font("Segoe UI Semibold", 9),
            left: 0, top: 2, gap: 6, iconSize: 12f, iconTop: 0, textTop: 0);
        pnlTarih.Controls.Add(tarihTitle);

        // Başlık label
        var lblBaslikLabel = new Label
        {
            Dock = DockStyle.Top, Height = 24,
            Text = L("note_title_placeholder"),
            Font = new Font("Segoe UI Semibold", 9),
            ForeColor = UIHelper.TextSecondary,
            Padding = new Padding(0, 6, 0, 0)
        };

        // Başlık input
        txtBaslik = new TextBox
        {
            Dock = DockStyle.Top, Height = 36,
            BackColor = UIHelper.BgInput, ForeColor = UIHelper.TextWhite,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI Semibold", 12),
            Text = mevcut?.Baslik ?? ""
        };

        var spacer = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = UIHelper.BgDark };

        // İçerik label
        var lblIcerikLabel = new Label
        {
            Dock = DockStyle.Top, Height = 24,
            Text = L("note_content_placeholder"),
            Font = new Font("Segoe UI Semibold", 9),
            ForeColor = UIHelper.TextSecondary,
            Padding = new Padding(0, 4, 0, 0)
        };

        // İçerik input
        txtIcerik = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = UIHelper.BgInput, ForeColor = Color.FromArgb(200, 210, 225),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10),
            Multiline = true, ScrollBars = ScrollBars.Vertical,
            Text = mevcut?.Icerik ?? ""
        };

        // ── Footer ──
        var pnlFooter = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = UIHelper.BgPanel };
        var sep = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = UIHelper.Divider };
        var btnKaydet = UIHelper.MakeButton(L("save_note"), UIHelper.AccentYellow, 0, 10, 130, 34);
        var btnIptal = UIHelper.MakeButton(L("cancel"), UIHelper.BtnDark, 0, 10, 100, 34);

        btnKaydet.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(txtBaslik.Text))
            {
                MessageBox.Show(L("title_empty"), L("warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Sonuc.Baslik = txtBaslik.Text.Trim();
            Sonuc.Icerik = txtIcerik.Text;
            if (mevcut == null) Sonuc.Tarih = DateTime.Now;
            DialogResult = DialogResult.OK;
            Close();
        };

        btnIptal.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        pnlFooter.Controls.AddRange(new Control[] { btnKaydet, btnIptal, sep });
        pnlFooter.Resize += (_, _) =>
        {
            btnKaydet.Left = pnlFooter.Width - btnKaydet.Width - 16;
            btnIptal.Left = btnKaydet.Left - btnIptal.Width - 8;
        };

        // Assemble (reverse order for Dock)
        pnlBody.Controls.Add(txtIcerik);
        pnlBody.Controls.Add(lblIcerikLabel);
        pnlBody.Controls.Add(spacer);
        pnlBody.Controls.Add(txtBaslik);
        pnlBody.Controls.Add(lblBaslikLabel);
        pnlBody.Controls.Add(pnlTarih);

        Controls.Add(pnlBody);
        Controls.Add(pnlFooter);
        Controls.Add(pnlHeader);
    }
}
