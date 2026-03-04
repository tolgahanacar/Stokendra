using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class NotlarPanel : UserControl
{
    private ListBox lstNotlar = new();
    private TextBox txtBaslik = new(), txtIcerik = new();
    private Button btnEkle = new(), btnSil = new(), btnKaydet = new();
    private Label lblTarih = new();
    private List<Not> _notlar = new();
    private Not? _secili = null;

    public NotlarPanel()
    {
        BackColor = UIHelper.BgDark;
        DoubleBuffered = true;

        // ── Header ──
        var pnlH = new Panel { Dock = DockStyle.Top, Height = 65, BackColor = UIHelper.BgDark };
        var lblTitle = new Label { Text = L("notes"), Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = UIHelper.TextWhite, Left = 28, Top = 14, AutoSize = true };
        var lblSub = new Label { Text = L("notes_subtitle"), Font = new Font("Segoe UI", 9), ForeColor = UIHelper.TextMuted, Left = 28, Top = 44, AutoSize = true };
        pnlH.Controls.AddRange(new Control[] { lblTitle, lblSub });

        // ── Toolbar ──
        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = UIHelper.BgPanel };
        btnEkle = UIHelper.MakeButton(L("new_note"), UIHelper.AccentYellow, 12, 8, 120, 30);
        btnSil  = UIHelper.MakeButton(L("delete"),   UIHelper.AccentRed,    140, 8, 80, 30);
        btnEkle.Click += (_, _) => YeniNot();
        btnSil.Click  += (_, _) => SilNot();
        pnlToolbar.Controls.AddRange(new Control[] { btnEkle, btnSil });

        // ── Sol panel (liste) ──
        var pnlLeft = new Panel { Dock = DockStyle.Left, Width = 280, BackColor = Color.FromArgb(17, 20, 28) };
        lstNotlar.Dock = DockStyle.Fill;
        lstNotlar.BackColor = Color.FromArgb(17, 20, 28);
        lstNotlar.ForeColor = UIHelper.TextSecondary;
        lstNotlar.Font = new Font("Segoe UI", 9);
        lstNotlar.BorderStyle = BorderStyle.None;
        lstNotlar.ItemHeight = 48;
        lstNotlar.DrawMode = DrawMode.OwnerDrawFixed;
        lstNotlar.DrawItem += DrawNotItem;
        lstNotlar.SelectedIndexChanged += (_, _) => SecNotYukle();
        pnlLeft.Controls.Add(lstNotlar);

        // ── Sağ panel (editör) ──
        var pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.BgDark, Padding = new Padding(20, 14, 20, 14) };
        var sep = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = UIHelper.Divider };

        lblTarih = new Label { Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 8), ForeColor = UIHelper.TextDim, Padding = new Padding(0, 4, 0, 0) };

        txtBaslik = new TextBox
        {
            Dock = DockStyle.Top, Height = 36,
            BackColor = UIHelper.BgCard, ForeColor = UIHelper.TextWhite,
            BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 14, FontStyle.Bold),
            PlaceholderText = L("note_title_placeholder")
        };

        var divBaslik = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = UIHelper.Divider };

        txtIcerik = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = UIHelper.BgDark, ForeColor = Color.FromArgb(190, 200, 218),
            BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10),
            Multiline = true, ScrollBars = ScrollBars.Vertical,
            PlaceholderText = L("note_content_placeholder")
        };

        var pnlSaveBar = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = UIHelper.BgDark };
        btnKaydet = UIHelper.MakeButton(L("save_note"), UIHelper.AccentYellow, 0, 4, 128, 32);
        btnKaydet.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        btnKaydet.Click += KaydetNot;
        pnlSaveBar.Controls.Add(btnKaydet);
        pnlSaveBar.Resize += (_, _) => { btnKaydet.Left = pnlSaveBar.Width - 140; };

        pnlRight.Controls.Add(txtIcerik);
        pnlRight.Controls.Add(divBaslik);
        pnlRight.Controls.Add(txtBaslik);
        pnlRight.Controls.Add(lblTarih);
        pnlRight.Controls.Add(sep);
        pnlRight.Controls.Add(pnlSaveBar);

        Controls.Add(pnlRight);
        Controls.Add(pnlLeft);
        Controls.Add(pnlToolbar);
        Controls.Add(pnlH);

        YukleNotlar();
    }

    private void DrawNotItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _notlar.Count) return;
        var n = _notlar[e.Index];
        bool sel = (e.State & DrawItemState.Selected) != 0;
        e.DrawBackground();
        var bg = sel ? Color.FromArgb(30, 41, 59) : (e.Index % 2 == 0 ? Color.FromArgb(17, 20, 28) : Color.FromArgb(20, 24, 34));
        using var brBg = new SolidBrush(bg);
        e.Graphics.FillRectangle(brBg, e.Bounds);
        if (sel)
        {
            using var accent = new SolidBrush(UIHelper.AccentYellow);
            e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Top, 3, e.Bounds.Height);
        }
        using var fTitle = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var fDate = new Font("Segoe UI", 7.5f);
        using var brW = new SolidBrush(UIHelper.TextPrimary);
        using var brG = new SolidBrush(UIHelper.TextDim);
        string title = n.Baslik.Length > 30 ? n.Baslik[..30] + "..." : n.Baslik;
        e.Graphics.DrawString(title, fTitle, brW, e.Bounds.Left + 14, e.Bounds.Top + 8);
        e.Graphics.DrawString(n.Tarih.ToString("dd.MM.yyyy HH:mm"), fDate, brG, e.Bounds.Left + 14, e.Bounds.Top + 28);
    }

    private void YukleNotlar()
    {
        _notlar = Program.DB!.NotlariGetir();
        lstNotlar.BeginUpdate();
        lstNotlar.Items.Clear();
        foreach (var n in _notlar) lstNotlar.Items.Add(n.Baslik);
        lstNotlar.EndUpdate();
        if (_notlar.Count > 0) lstNotlar.SelectedIndex = 0;
        else TemizleEditor();
    }

    private void SecNotYukle()
    {
        if (lstNotlar.SelectedIndex < 0 || lstNotlar.SelectedIndex >= _notlar.Count) return;
        _secili = _notlar[lstNotlar.SelectedIndex];
        txtBaslik.Text = _secili.Baslik;
        txtIcerik.Text = _secili.Icerik;
        lblTarih.Text = _secili.Tarih.ToString("dd MMMM yyyy, HH:mm");
    }

    private void TemizleEditor() { _secili = null; txtBaslik.Text = ""; txtIcerik.Text = ""; lblTarih.Text = ""; }
    private void YeniNot() { _secili = null; txtBaslik.Text = ""; txtIcerik.Text = ""; txtBaslik.Focus(); lblTarih.Text = L("new_note_label"); }

    private void KaydetNot(object? s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtBaslik.Text)) { MessageBox.Show(L("title_empty")); return; }
        if (_secili == null)
            Program.DB!.NotEkle(new Not { Tarih = DateTime.Now, Baslik = txtBaslik.Text.Trim(), Icerik = txtIcerik.Text });
        else { _secili.Baslik = txtBaslik.Text.Trim(); _secili.Icerik = txtIcerik.Text; Program.DB!.NotGuncelle(_secili); }
        YukleNotlar();
    }

    private void SilNot()
    {
        if (_secili == null) { MessageBox.Show(L("select_note_first")); return; }
        if (MessageBox.Show(L("confirm_note_delete"), L("confirm_delete_title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        { Program.DB!.NotSil(_secili.Id); TemizleEditor(); YukleNotlar(); }
    }
}
