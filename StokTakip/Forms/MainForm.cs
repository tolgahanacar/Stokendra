using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public sealed class MainForm : Form
{
    private Panel pnlContent = new();
    private Panel pnlSidebar = new();
    private readonly List<Panel> _sidebarItems = new();

    private static readonly (string key, Color accent)[] NavItems = {
        ("menu_dashboard",       UIHelper.AccentCyan),
        ("menu_reports",         UIHelper.AccentBlue),
        ("menu_stock_cards",     UIHelper.AccentGreen),
        ("menu_stocks",          UIHelper.AccentOrange),
        ("menu_stock_movements", UIHelper.AccentPurple),
        ("menu_services",        Color.FromArgb(231, 76, 60)),
        ("menu_notes",           UIHelper.AccentCyan),
        ("menu_departments",     UIHelper.AccentYellow),
        ("menu_settings",        UIHelper.TextSecondary),
    };

    public MainForm()
    {
        Text = L("app_title");
        Size = new Size(1320, 800);
        MinimumSize = new Size(1050, 620);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = UIHelper.BgDark;
        DoubleBuffered = true;
        BuildSidebar();
        BuildContent();
        ShowPage("dashboard");
        Load += (_, _) => _ = UpdateChecker.CheckSilentAsync();
        Load += (_, _) => _ = BackupManager.CheckWeeklyBackupAsync();
    }

    private void BuildSidebar()
    {
        pnlSidebar = new Panel { Dock = DockStyle.Left, Width = 240, BackColor = UIHelper.BgSidebar };

        // Logo — lighter background for visibility, text-based logo
        var pnlLogo = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Color.FromArgb(22, 28, 42) };
        // Logoyu bulanıklaştırmadan yüksek kalitede çizmek için Custom Paint
        var picLogo = new PictureBox { Width = 40, Height = 40, Left = 16, Top = 14, BackColor = Color.FromArgb(35, 50, 80) };
        picLogo.Paint += (s, e) => {
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            if (picLogo.Image != null) e.Graphics.DrawImage(picLogo.Image, new Rectangle(0, 0, picLogo.Width, picLogo.Height));
        };
        try { picLogo.Image = Icon.ExtractAssociatedIcon(AppDomain.CurrentDomain.FriendlyName)?.ToBitmap() ?? Image.FromFile("StokTakip.ico"); } catch { try { picLogo.Image = Image.FromFile("StokTakip.ico"); } catch { } }
        var lblTitle = new Label { Text = "Stokendra", Font = new Font("Segoe UI", 15), ForeColor = UIHelper.TextWhite, Left = 62, Top = 14, AutoSize = true };
        var lblVer = new Label { Text = "v" + Application.ProductVersion, Font = new Font("Segoe UI", 10), ForeColor = UIHelper.AccentCyan, Left = 62, Top = 38, AutoSize = true };
        pnlLogo.Controls.AddRange(new Control[] { picLogo, lblTitle, lblVer });

        var divTop = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = UIHelper.SidebarDivider };

        var navPanel = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.BgSidebar, AutoScroll = true };
        navPanel.Controls.Add(new Label { Text = L("menu"), Left = 20, Top = 10, AutoSize = true, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = UIHelper.TextDim });

        int y = 36;
        foreach (var (key, accent) in NavItems)
        {
            var item = UIHelper.MakeSidebarItem("", L(key), accent, y, key == "menu_dashboard");
            item.Tag = key; item.Click += (_, _) => ShowPage(key);
            navPanel.Controls.Add(item); _sidebarItems.Add(item); y += 46;
        }

        // Copyright at bottom
        var pnlFooter = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = UIHelper.BgSidebar };
        pnlFooter.Controls.AddRange(new Control[] {
            new Label { Text = "© 2026 Tolgahan Acar", Font = new Font("Segoe UI", 10), ForeColor = UIHelper.TextDim, Left = 20, Top = 8, AutoSize = true },
            new Label { Text = "Tüm hakları saklıdır.", Font = new Font("Segoe UI", 8), ForeColor = Color.FromArgb(60, 70, 90), Left = 20, Top = 26, AutoSize = true }
        });

        pnlSidebar.Controls.AddRange(new Control[] { navPanel, divTop, pnlLogo, pnlFooter });

        // Accent strip
        Controls.Add(new Panel { Dock = DockStyle.Left, Width = 2, BackColor = UIHelper.AccentCyan });
        Controls.Add(pnlSidebar);
    }

    private void BuildContent()
    {
        pnlContent = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.BgDark };
        Controls.Add(pnlContent); pnlContent.BringToFront();
    }

    private void ShowPage(string key)
    {
        UpdateSidebarSelection(key);
        pnlContent.SuspendLayout(); 
        
        // FIX: Dispose old controls to prevent memory leaks
        foreach (Control c in pnlContent.Controls)
        {
            if (!c.IsDisposed) c.Dispose();
        }
        pnlContent.Controls.Clear();

        Control content = key switch {
            "menu_dashboard" => new DashboardPanel(), "menu_reports" => new RaporlarPanel(),
            "menu_stock_cards" => new StokKartlariPanel(), "menu_stocks" => new StoklarPanel(),
            "menu_stock_movements" => new StokHareketPanel(), "menu_services" => new ServislerPanel(), "menu_notes" => new NotlarPanel(), "menu_departments" => new DepartmanlarPanel(),
            "menu_settings" => new AyarlarPanel(), _ => new DashboardPanel()
        };
        content.Dock = DockStyle.Fill; pnlContent.Controls.Add(content); pnlContent.ResumeLayout(true);
    }

    private void UpdateSidebarSelection(string activeKey)
    {
        for (int i = 0; i < _sidebarItems.Count && i < NavItems.Length; i++)
        {
            var pnl = _sidebarItems[i]; var nav = NavItems[i]; bool a = nav.key == activeKey;
            pnl.BackColor = a ? UIHelper.SidebarActive : Color.Transparent;
            if (pnl.Controls.Count < 3) continue;
            
            if (pnl.Controls[0] is Panel ab) ab.BackColor = a ? nav.accent : Color.Transparent;
            if (pnl.Controls[1] is Label li) li.ForeColor = a ? nav.accent : UIHelper.TextSecondary;
            if (pnl.Controls[2] is Label lt)
            {
                lt.ForeColor = a ? UIHelper.TextWhite : UIHelper.TextSecondary;
                lt.Font = new Font("Segoe UI Semibold", 10.5f, a ? FontStyle.Bold : FontStyle.Regular);
            }
        }
    }
}
