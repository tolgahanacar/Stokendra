using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Forms;

public class MainForm : Form
{
    private Panel pnlContent = new();
    private Panel pnlSidebar = new();
    private readonly List<Panel> _sidebarItems = new();

    private static readonly (string icon, string key, System.Drawing.Color accent)[] NavItems = {
        ("📊", "dashboard",       UIHelper.AccentCyan),
        ("📈", "reports",         UIHelper.AccentBlue),
        ("📋", "stock_cards",     UIHelper.AccentGreen),
        ("📦", "stocks",          UIHelper.AccentOrange),
        ("🔄", "stock_movements", UIHelper.AccentPurple),
        ("🛠", "services",        System.Drawing.Color.FromArgb(231, 76, 60)),
        ("📝", "notes",           UIHelper.AccentCyan),
        ("🏢", "departments",     UIHelper.AccentYellow),
        ("⚙️", "settings",        UIHelper.TextSecondary),
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
    }

    private void BuildSidebar()
    {
        pnlSidebar = new Panel { Dock = DockStyle.Left, Width = 240, BackColor = UIHelper.BgSidebar };

        // Logo — lighter background for visibility, text-based logo
        var pnlLogo = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Color.FromArgb(22, 28, 42) };
        var lblBrand = new Label
        {
            Text = "S",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = UIHelper.AccentCyan,
            BackColor = Color.FromArgb(35, 50, 80),
            Width = 40, Height = 40, Left = 16, Top = 14,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var lblTitle = new Label { Text = L("app_name"), Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = UIHelper.TextWhite, Left = 62, Top = 14, AutoSize = true };
        var lblVer = new Label { Text = L("app_version"), Font = new Font("Segoe UI Semibold", 8), ForeColor = UIHelper.AccentCyan, Left = 62, Top = 38, AutoSize = true };
        pnlLogo.Controls.AddRange(new Control[] { lblBrand, lblTitle, lblVer });

        var divTop = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = UIHelper.SidebarDivider };

        var navPanel = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.BgSidebar, AutoScroll = true };
        navPanel.Controls.Add(new Label { Text = L("menu"), Left = 20, Top = 10, AutoSize = true, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = UIHelper.TextDim });

        int y = 36;
        foreach (var (icon, key, accent) in NavItems)
        {
            var item = UIHelper.MakeSidebarItem(icon, L(key), accent, y, key == "dashboard");
            item.Tag = key; item.Click += (_, _) => ShowPage(key);
            navPanel.Controls.Add(item); _sidebarItems.Add(item); y += 46;
        }

        // Copyright at bottom
        var pnlFooter = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = UIHelper.BgSidebar };
        pnlFooter.Controls.AddRange(new Control[] {
            new Label { Text = "© 2026 Tolgahan Acar", Font = new Font("Segoe UI", 7.5f), ForeColor = UIHelper.TextDim, Left = 20, Top = 8, AutoSize = true },
            new Label { Text = "Tüm hakları saklıdır.", Font = new Font("Segoe UI", 7), ForeColor = Color.FromArgb(60, 70, 90), Left = 20, Top = 26, AutoSize = true }
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
        pnlContent.SuspendLayout(); pnlContent.Controls.Clear();
        Control content = key switch {
            "dashboard" => new DashboardPanel(), "reports" => new RaporlarPanel(),
            "stock_cards" => new StokKartlariPanel(), "stocks" => new StoklarPanel(),
            "stock_movements" => new StokHareketPanel(), "services" => new ServislerPanel(), "notes" => new NotlarPanel(), "departments" => new DepartmanlarPanel(),
            "settings" => new AyarlarPanel(), _ => new DashboardPanel()
        };
        content.Dock = DockStyle.Fill; pnlContent.Controls.Add(content); pnlContent.ResumeLayout(true);
    }

    private void UpdateSidebarSelection(string activeKey)
    {
        for (int i = 0; i < _sidebarItems.Count && i < NavItems.Length; i++)
        {
            var pnl = _sidebarItems[i]; bool a = NavItems[i].key == activeKey;
            pnl.BackColor = a ? UIHelper.SidebarActive : Color.Transparent;
            if (pnl.Controls.Count < 3) continue;
            if (pnl.Controls[0] is Panel ab) ab.BackColor = a ? NavItems[i].accent : Color.Transparent;
            if (pnl.Controls[1] is Label li) li.ForeColor = a ? NavItems[i].accent : UIHelper.TextMuted;
            if (pnl.Controls[2] is Label lt) { lt.ForeColor = a ? UIHelper.TextWhite : UIHelper.TextSecondary; lt.Font = new Font("Segoe UI Semibold", 10.5f, a ? FontStyle.Bold : FontStyle.Regular); }
        }
    }
}
