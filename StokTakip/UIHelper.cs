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
    public static readonly Color AccentBlue   = Color.FromArgb(96, 165, 250);
    public static readonly Color AccentGreen  = Color.FromArgb(52, 211, 153);
    public static readonly Color AccentYellow = Color.FromArgb(251, 191, 36);
    public static readonly Color AccentRed    = Color.FromArgb(248, 113, 113);
    public static readonly Color AccentPurple = Color.FromArgb(167, 139, 250);
    public static readonly Color AccentCyan   = Color.FromArgb(34, 211, 238);
    public static readonly Color AccentOrange = Color.FromArgb(251, 146, 60);

    // Metin — yüksek kontrast
    public static readonly Color TextWhite     = Color.FromArgb(250, 250, 255);
    public static readonly Color TextPrimary   = Color.FromArgb(235, 240, 250);
    public static readonly Color TextSecondary = Color.FromArgb(170, 180, 200);
    public static readonly Color TextMuted     = Color.FromArgb(120, 135, 160);
    public static readonly Color TextDim       = Color.FromArgb(85, 100, 125);

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
    public static readonly Font FontGrid       = new("Segoe UI Semibold", 9.5f);
    public static readonly Font FontGridHeader = new("Segoe UI", 9f, FontStyle.Bold);
    public static readonly Font FontInput      = new("Segoe UI Semibold", 10f);
    public static readonly Font FontButton     = new("Segoe UI Semibold", 9f);
    public static readonly Font FontLabel      = new("Segoe UI Semibold", 9.5f);
    public static readonly Font FontTitle      = new("Segoe UI", 20f, FontStyle.Bold);
    public static readonly Font FontSubtitle   = new("Segoe UI", 10f);

    // ═══ Türkçe büyük harf dönüşümü ═══
    private static readonly System.Globalization.CultureInfo TrCulture = new System.Globalization.CultureInfo("tr-TR");

    /// <summary>Türkçe karakterleri doğru büyüten ToUpper (ı→I, i→İ vs.)</summary>
    public static string ToUpperTr(this string s) => s.ToUpper(TrCulture);

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
        c.Font = FontInput; c.FlatStyle = FlatStyle.Popup;
    }

    public static void StyleDatePicker(DateTimePicker d)
    {
        d.CalendarMonthBackground = BgInput; d.CalendarForeColor = TextPrimary;
        d.Font = new Font("Segoe UI Semibold", 9);
    }

    // ═══ PREMIUM BUTON (İkon Destekli) ═══
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
        btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bg, 0.25f);
        btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(bg, 0.15f);
        
        btn.Paint += (s, e) => {
            var g = e.Graphics;
            using var p = new Pen(Color.FromArgb(40, Color.White), 1);
            g.DrawRectangle(p, 0, 0, btn.Width - 1, btn.Height - 1);
        };
        
        return btn;
    }

    /// <summary>FlowLayoutPanel uyumlu buton (pozisyonsuz)</summary>
    public static Button MakeFlowButton(string text, Color bg, int width = 120, int height = 30)
    {
        return MakeButton(text, bg, 0, 0, width, height);
    }

    // ═══ MODERN CARD PANEL ═══
    public static Panel MakeCardPanel(int width = 500, int height = 150)
    {
        var pnl = new Panel
        {
            Width = width, Height = height,
            BackColor = BgCard, Padding = new Padding(20)
        };
        
        pnl.Paint += (s, e) => {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var p = new Pen(Color.FromArgb(45, Color.White), 1);
            // Draw a subtle border
            g.DrawRectangle(p, 0, 0, pnl.Width - 1, pnl.Height - 1);
        };
        
        return pnl;
    }

    public static Panel MakeSettingsGroup(string title, string icon, Color accent, out FlowLayoutPanel body, int width = 550)
    {
        var card = MakeCardPanel(width, 60); 
        card.AutoSize = true;
        card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        card.Padding = new Padding(16, 12, 16, 16);
        card.Margin = new Padding(0, 0, 12, 12);

        var lblTitle = new Label 
        { 
            Text = $"{icon}  {title}", 
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), 
            ForeColor = accent, 
            AutoSize = true, 
            Location = new Point(16, 12)
        };
        
        body = new FlowLayoutPanel
        {
            Location = new Point(16, 40),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Width = width - 32,
            Padding = new Padding(0, 0, 0, 5)
        };

        card.Controls.Add(body);
        card.Controls.Add(lblTitle);
        
        return card;
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
        var lblT = new Label { Text = title, Left = 14, Top = 10, AutoSize = true, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = TextMuted };
        var lblV = new Label { Text = value, Left = 14, Top = 30, AutoSize = true, Font = new Font("Segoe UI", 22, FontStyle.Bold), ForeColor = accent };
        var lblS = new Label { Text = subtitle, Left = 14, Top = height - 24, AutoSize = true, Font = new Font("Segoe UI Semibold", 8), ForeColor = TextDim };
        card.Controls.AddRange(new Control[] { accentLine, lblT, lblV, lblS });

        card.MouseEnter += (_, _) => card.BackColor = BgHover;
        card.MouseLeave += (_, _) => card.BackColor = BgCard;
        foreach (Control c in card.Controls)
        {
            c.MouseEnter += (_, _) => card.BackColor = BgHover;
            c.MouseLeave += (_, _) => card.BackColor = BgCard;
        }
        return card;
    }

    public static Panel MakeStatCard(string title, string value, string subtitle, Color accent, int width = 185, int height = 96) => MakeStatCard(title, value, subtitle, accent, 0, 0, width, height);

    // ═══ TOOLBAR (FlowLayoutPanel) ═══
    public static FlowLayoutPanel MakeToolbar(int height = 44)
    {
        var pnl = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = height, BackColor = BgToolbar,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
            Padding = new Padding(12, 6, 8, 4), AutoSize = false
        };
        pnl.Paint += (s, e) => {
            using var p = new Pen(SidebarDivider, 1);
            e.Graphics.DrawLine(p, 0, pnl.Height - 1, pnl.Width, pnl.Height - 1);
        };
        return pnl;
    }

    // ═══ FORM LABEL ═══
    public static void AddFormLabel(Control parent, string text, int top, int left = 16, int width = 120)
    {
        parent.Controls.Add(new Label
        {
            Text = text, Left = left, Top = top + 3, Width = width,
            AutoSize = true, Font = FontLabel, ForeColor = TextSecondary
        });
    }

    public static TextBox MakeSearchBox(string placeholder, int width = 220)
    {
        var t = new TextBox { Width = width, Height = 30, AutoSize = false, Margin = new Padding(4, 3, 8, 3) };
        StyleTextBox(t); t.PlaceholderText = placeholder;
        return t;
    }

    // ═══ SIDEBAR ITEM ═══
    public static Panel MakeSidebarItem(string icon, string text, Color accent, int top, bool active = false)
    {
        var pnl = new Panel { Left = 0, Top = top, Width = 230, Height = 46, BackColor = active ? SidebarActive : Color.Transparent, Cursor = Cursors.Hand, Tag = text };
        var accentBar = new Panel { Left = 0, Top = 0, Width = 4, Height = 46, BackColor = active ? accent : Color.Transparent };
        
        var lblIcon = new Label { Text = icon, Left = 16, Top = 12, AutoSize = true, Font = new Font("Segoe UI", 11f), ForeColor = active ? accent : TextSecondary, Cursor = Cursors.Hand };
        var lblText = new Label { Text = text, Left = 46, Top = 12, AutoSize = true, Font = new Font("Segoe UI Semibold", 10.5f, active ? FontStyle.Bold : FontStyle.Regular), ForeColor = active ? TextWhite : TextSecondary, Cursor = Cursors.Hand };
        
        pnl.MouseEnter += (_, _) => { if (!active) pnl.BackColor = SidebarHover; };
        pnl.MouseLeave += (_, _) => { if (!active) pnl.BackColor = Color.Transparent; };
        
        EventHandler bubble = (s, e) => {
            pnl.GetType().GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(pnl, new object[] { e });
        };
        lblText.Click += bubble; lblIcon.Click += bubble;

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

    /// <summary>Blend two colors for smooth transitions</summary>
    public static Color BlendColor(Color from, Color to, float amount)
    {
        int r = (int)(from.R + (to.R - from.R) * amount);
        int g = (int)(from.G + (to.G - from.G) * amount);
        int b = (int)(from.B + (to.B - from.B) * amount);
        return Color.FromArgb(Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255));
    }

    /// <summary>Creates a stylized label with an optional icon (via emoji text)</summary>
    public static Label MakeIconTitle(string text, Color color, Font font, int left, int top, int gap, float iconSize, int iconTop, int textTop)
    {
        return new Label { Text = text, ForeColor = color, Font = font, Left = left, Top = top, AutoSize = true };
    }
}

/// <summary>Custom-painted card with gradient background, accent glow, and hover effects.</summary>
public class GlowCard : Panel
{
    private Color _accent = UIHelper.AccentBlue;
    private bool _hovered = false;
    private int _cornerRadius = 10;

    public Color AccentColor { get => _accent; set { _accent = value; Invalidate(); } }
    public int CornerRadius { get => _cornerRadius; set { _cornerRadius = value; Invalidate(); } }

    public GlowCard()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = UIHelper.BgCard;
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e)
    {
        var pos = PointToClient(Cursor.Position);
        if (!ClientRectangle.Contains(pos)) { _hovered = false; Invalidate(); }
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedRect(rect, _cornerRadius);

        var bgTop = _hovered ? UIHelper.BlendColor(UIHelper.BgCard, _accent, 0.10f) : UIHelper.BgCard;
        var bgBot = _hovered ? UIHelper.BlendColor(Color.FromArgb(20, 24, 34), _accent, 0.05f) : Color.FromArgb(20, 24, 34);
        using var bgBrush = new System.Drawing.Drawing2D.LinearGradientBrush(rect, bgTop, bgBot, 90f);
        g.FillPath(bgBrush, path);

        var glowRect = new Rectangle(0, 4, 4, Height - 8);
        using var glowBrush = new SolidBrush(_hovered ? _accent : Color.FromArgb(180, _accent));
        g.FillRectangle(glowBrush, glowRect);

        using var borderPen = new Pen(Color.FromArgb(_hovered ? 50 : 22, _accent), 1);
        g.DrawPath(borderPen, path);
    }

    internal static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var gp = new System.Drawing.Drawing2D.GraphicsPath();
        if (d > 0)
        {
            gp.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            gp.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            gp.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            gp.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        }
        gp.CloseFigure();
        return gp;
    }
}

/// <summary>Section panel with gradient header</summary>
public class SectionPanel : Panel
{
    private string _title = "";
    private Color _accent = UIHelper.StokWarning;

    public string Title { get => _title; set { _title = value; Invalidate(); } }
    public Color AccentColor { get => _accent; set { _accent = value; Invalidate(); } }

    public int HeaderHeight { get; set; } = 42;

    public SectionPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = UIHelper.BgDark;
        Padding = new Padding(0, 42, 0, 0);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var bgBrush = new SolidBrush(UIHelper.BgDark);
        g.FillRectangle(bgBrush, ClientRectangle);

        var headerRect = new Rectangle(0, 0, Width, HeaderHeight);
        using var hdrBrush = new System.Drawing.Drawing2D.LinearGradientBrush(headerRect, Color.FromArgb(30, _accent.R, _accent.G, _accent.B), UIHelper.BgDark, 0f);
        g.FillRectangle(hdrBrush, headerRect);

        using var linePen = new Pen(Color.FromArgb(60, _accent), 2);
        g.DrawLine(linePen, 8, HeaderHeight - 1, Width - 8, HeaderHeight - 1);

        using var fTitle = new Font("Segoe UI", 12.5f, FontStyle.Bold);
        using var accentBrush = new SolidBrush(_accent);
        g.DrawString(_title, fTitle, accentBrush, 12, 10);

        base.OnPaint(e);
    }
}
