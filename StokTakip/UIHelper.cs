using static StokTakip.LocalizationManager;

namespace StokTakip;

public static class UIHelper
{
    // ═══ RENK PALETİ (Premium Dark — Yüksek Kontrast) ═══
    public static readonly Color BgDark       = Color.FromArgb(15, 17, 23);
    public static readonly Color BgSidebar    = Color.FromArgb(18, 21, 30);
    public static readonly Color BgPanel      = Color.FromArgb(22, 26, 38);
    public static readonly Color BgCard       = Color.FromArgb(26, 31, 44);
    public static readonly Color BgInput      = Color.FromArgb(30, 36, 52);
    public static readonly Color BgAltRow     = Color.FromArgb(19, 22, 32);
    public static readonly Color BgHover      = Color.FromArgb(32, 40, 58);
    public static readonly Color BgToolbar    = Color.FromArgb(20, 24, 36);

    // Aksan renkleri — canlı ve okunaklı
    public static readonly Color AccentBlue   = Color.FromArgb(96, 165, 250);   // Daha parlak mavi
    public static readonly Color AccentGreen  = Color.FromArgb(52, 211, 153);   // Canlı yeşil
    public static readonly Color AccentYellow = Color.FromArgb(251, 191, 36);
    public static readonly Color AccentRed    = Color.FromArgb(248, 113, 113);
    public static readonly Color AccentPurple = Color.FromArgb(167, 139, 250);
    public static readonly Color AccentCyan   = Color.FromArgb(34, 211, 238);
    public static readonly Color AccentOrange = Color.FromArgb(251, 146, 60);

    // Metin — yüksek kontrast
    public static readonly Color TextWhite    = Color.FromArgb(250, 250, 255);
    public static readonly Color TextPrimary  = Color.FromArgb(235, 240, 250);  // Daha parlak
    public static readonly Color TextSecondary = Color.FromArgb(170, 180, 200); // Daha okunabilir
    public static readonly Color TextMuted    = Color.FromArgb(120, 135, 160);
    public static readonly Color TextDim      = Color.FromArgb(85, 100, 125);

    public static readonly Color GridLine     = Color.FromArgb(35, 42, 60);
    public static readonly Color Divider      = Color.FromArgb(38, 48, 70);
    public static readonly Color SelectionBg  = Color.FromArgb(40, 72, 130);
    public static readonly Color BtnDark      = Color.FromArgb(55, 70, 95);
    public static readonly Color BtnMid       = Color.FromArgb(75, 90, 115);
    public static readonly Color BtnBarBg     = Color.FromArgb(55, 70, 95);
    public static readonly Color StokWarning  = Color.FromArgb(248, 113, 113);
    public static readonly Color StokLow      = Color.FromArgb(251, 191, 36);

    public static readonly Color SidebarActive  = Color.FromArgb(30, 41, 59);
    public static readonly Color SidebarHover   = Color.FromArgb(26, 36, 54);
    public static readonly Color SidebarDivider = Color.FromArgb(35, 48, 68);

    // ═══ FONT ═══
    public static readonly Font FontGrid      = new("Segoe UI Semibold", 9.5f);
    public static readonly Font FontGridHeader = new("Segoe UI", 9f, FontStyle.Bold);
    public static readonly Font FontInput     = new("Segoe UI Semibold", 10f);
    public static readonly Font FontButton    = new("Segoe UI Semibold", 9f);
    public static readonly Font FontLabel     = new("Segoe UI Semibold", 9.5f);
    public static readonly Font FontTitle     = new("Segoe UI", 20f, FontStyle.Bold);
    public static readonly Font FontSubtitle  = new("Segoe UI", 10f);

    // ═══ GRID STİLLENDİRME ═══
    public static void StyleGrid(DataGridView g, bool multiSelect = false)
    {
        g.ReadOnly = true;
        g.MultiSelect = multiSelect;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.AllowUserToAddRows = false;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        g.BackgroundColor = BgDark;
        g.BorderStyle = BorderStyle.None;
        g.RowHeadersVisible = false;
        g.GridColor = GridLine;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

        g.DefaultCellStyle.BackColor = BgDark;
        g.DefaultCellStyle.ForeColor = TextPrimary;
        g.DefaultCellStyle.SelectionBackColor = SelectionBg;
        g.DefaultCellStyle.SelectionForeColor = TextWhite;
        g.DefaultCellStyle.Font = FontGrid;
        g.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);

        g.ColumnHeadersDefaultCellStyle.BackColor = BgPanel;
        g.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
        g.ColumnHeadersDefaultCellStyle.Font = FontGridHeader;
        g.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        g.ColumnHeadersHeight = 36;
        g.RowTemplate.Height = 34;
        g.EnableHeadersVisualStyles = false;

        g.AlternatingRowsDefaultCellStyle.BackColor = BgAltRow;
        g.AlternatingRowsDefaultCellStyle.ForeColor = TextPrimary;
    }

    // ═══ INPUT STİLLERİ ═══
    public static void StyleTextBox(TextBox t)
    {
        t.BackColor = BgInput; t.ForeColor = TextPrimary;
        t.BorderStyle = BorderStyle.FixedSingle; t.Font = FontInput;
    }

    public static void StyleComboBox(ComboBox c)
    {
        c.BackColor = BgInput; c.ForeColor = TextPrimary;
        c.Font = FontInput; c.FlatStyle = FlatStyle.Flat;
    }

    public static void StyleDatePicker(DateTimePicker d)
    {
        d.CalendarMonthBackground = BgInput; d.CalendarForeColor = TextPrimary;
        d.Font = new Font("Segoe UI Semibold", 9);
    }

    // ═══ PREMIUM BUTON ═══
    public static Button MakeButton(string text, Color bg, int left, int top = 10, int width = 120, int height = 34)
    {
        var btn = new Button
        {
            Text = text, Left = left, Top = top, Width = width, Height = height,
            BackColor = bg, ForeColor = TextWhite, FlatStyle = FlatStyle.Flat,
            Font = FontButton, Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(2, 2, 2, 2)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bg, 0.15f);
        btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(bg, 0.1f);
        return btn;
    }

    /// <summary>FlowLayoutPanel uyumlu buton (pozisyonsuz)</summary>
    public static Button MakeFlowButton(string text, Color bg, int width = 120, int height = 32)
    {
        return MakeButton(text, bg, 0, 0, width, height);
    }

    // ═══ FORM LABEL ═══
    public static void AddFormLabel(Control parent, string text, int top, int left = 16, int width = 120)
    {
        parent.Controls.Add(new Label { Text = text, Left = left, Top = top + 3, Width = width, Font = FontLabel, ForeColor = TextSecondary });
    }

    // ═══ STOK RENK ═══
    public static Color StokRengi(double miktar)
    {
        if (miktar <= 0) return StokWarning;
        if (miktar <= 3) return StokLow;
        return AccentGreen;
    }

    public static string FormatMiktar(double miktar)
    {
        if (miktar == Math.Floor(miktar)) return ((int)miktar).ToString();
        return miktar.ToString("N2");
    }

    // ═══ STAT CARD (Dashboard) ═══
    public static Panel MakeStatCard(string title, string value, string subtitle, Color accent, int left, int top, int width = 190, int height = 100)
    {
        var card = new Panel { Left = left, Top = top, Width = width, Height = height, BackColor = BgCard, Margin = new Padding(4) };
        var accentLine = new Panel { Left = 0, Top = 0, Width = 4, Height = height, BackColor = accent };
        var lblT = new Label { Text = title.ToUpperInvariant(), Left = 14, Top = 10, AutoSize = true, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = TextMuted };
        var lblV = new Label { Text = value, Left = 14, Top = 30, AutoSize = true, Font = new Font("Segoe UI", 22, FontStyle.Bold), ForeColor = accent };
        var lblS = new Label { Text = subtitle, Left = 14, Top = height - 24, AutoSize = true, Font = new Font("Segoe UI Semibold", 8), ForeColor = TextDim };
        card.Controls.AddRange(new Control[] { accentLine, lblT, lblV, lblS });

        card.MouseEnter += (_, _) => card.BackColor = BgHover;
        card.MouseLeave += (_, _) => card.BackColor = BgCard;
        foreach (Control c in card.Controls) { c.MouseEnter += (_, _) => card.BackColor = BgHover; c.MouseLeave += (_, _) => card.BackColor = BgCard; }
        return card;
    }

    public static Panel MakeStatCard(string title, string value, string subtitle, Color accent, int width = 185, int height = 96)
    {
        return MakeStatCard(title, value, subtitle, accent, 0, 0, width, height);
    }

    // ═══ TOOLBAR (FlowLayoutPanel) ═══
    public static FlowLayoutPanel MakeToolbar(int height = 44)
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = height, BackColor = BgToolbar,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
            Padding = new Padding(8, 6, 8, 4), AutoSize = false
        };
    }

    // ═══ SEARCH BOX ═══
    public static TextBox MakeSearchBox(string placeholder, int width = 220)
    {
        var t = new TextBox { Width = width, Height = 28, Margin = new Padding(4, 2, 8, 2) };
        StyleTextBox(t); t.PlaceholderText = placeholder;
        return t;
    }

    // ═══ SIDEBAR ITEM ═══
    public static Panel MakeSidebarItem(string icon, string text, Color accent, int top, bool active = false)
    {
        var pnl = new Panel { Left = 0, Top = top, Width = 240, Height = 46, BackColor = active ? SidebarActive : Color.Transparent, Cursor = Cursors.Hand, Tag = text };
        var accentBar = new Panel { Left = 0, Top = 0, Width = 4, Height = 46, BackColor = active ? accent : Color.Transparent };
        var lblIcon = new Label { Text = icon, Left = 20, Top = 10, Width = 32, Height = 28, Font = new Font("Segoe UI", 14), ForeColor = active ? accent : TextMuted, Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleCenter };
        var lblText = new Label { Text = text, Left = 56, Top = 12, AutoSize = true, Font = new Font("Segoe UI Semibold", 10.5f, active ? FontStyle.Bold : FontStyle.Regular), ForeColor = active ? TextWhite : TextSecondary, Cursor = Cursors.Hand };

        pnl.MouseEnter += (_, _) => { if (!active) pnl.BackColor = SidebarHover; };
        pnl.MouseLeave += (_, _) => { if (!active) pnl.BackColor = Color.Transparent; };
        lblIcon.MouseEnter += (_, _) => { if (!active) pnl.BackColor = SidebarHover; };
        lblIcon.MouseLeave += (_, _) => { if (!active) pnl.BackColor = Color.Transparent; };
        lblText.MouseEnter += (_, _) => { if (!active) pnl.BackColor = SidebarHover; };
        lblText.MouseLeave += (_, _) => { if (!active) pnl.BackColor = Color.Transparent; };

        EventHandler bubble = (s, e) => { pnl.GetType().GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(pnl, new object[] { e }); };
        lblIcon.Click += bubble; lblText.Click += bubble;

        pnl.Controls.AddRange(new Control[] { accentBar, lblIcon, lblText });
        return pnl;
    }

    // ═══ HEADER PANELİ ═══
    public static Panel MakeHeader(string title, string subtitle = "")
    {
        var pnl = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = BgDark, Padding = new Padding(28, 0, 28, 0) };
        pnl.Controls.Add(new Label { Text = title, Font = FontTitle, ForeColor = TextWhite, Left = 28, Top = 6, AutoSize = true });
        if (!string.IsNullOrEmpty(subtitle))
            pnl.Controls.Add(new Label { Text = subtitle, Font = new Font("Segoe UI Semibold", 10), ForeColor = TextSecondary, Left = 28, Top = 38, AutoSize = true });
        return pnl;
    }

    public static void AddTooltip(Control control, string text)
    {
        var tip = new ToolTip { BackColor = BgCard, ForeColor = TextPrimary, InitialDelay = 300, ReshowDelay = 200 };
        tip.SetToolTip(control, text);
    }
}
