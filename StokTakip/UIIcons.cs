using System.Collections.Generic;
using System.Drawing.Imaging;

namespace StokTakip;

public static class UIIcons
{
    private static readonly string[] FontCandidates =
    {
        "Segoe MDL2 Assets",
        "Segoe Fluent Icons",
        "Segoe UI Symbol",
        "Segoe UI Emoji"
    };

    private static readonly string? IconFontName = FindIconFont();
    public static bool HasIconFont => !string.IsNullOrEmpty(IconFontName);

    private static readonly Dictionary<string, Image> ImageCache = new(StringComparer.Ordinal);

    private sealed class ButtonIconInfo
    {
        public string OriginalText = "";
        public bool EventsHooked;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Button, ButtonIconInfo> ButtonInfos = new();

    // Legacy emoji/symbol -> MDL2/Fluent glyph mapping
    private static readonly Dictionary<string, string> LegacyMap = new(StringComparer.Ordinal)
    {
        // Actions
        ["✕"] = "\uE711", ["✖"] = "\uE711",
        ["＋"] = "\uE710", ["+"] = "\uE710",
        ["✏️"] = "\uE70F", ["✏"] = "\uE70F",
        ["🖨"] = "\uE749",
        ["🗑"] = "\uE74D",
        ["🔍"] = "\uE721",
        ["📥"] = "\uE896",
        ["📤"] = "\uE898",
        ["📊"] = "\uE9D2",
        ["📈"] = "\uE9D2",
        ["📋"] = "\uE8A5",

        // Status / alerts
        ["⚡"] = "\uE945",
        ["⚠"] = "\uE7BA",
        ["🔴"] = "\uE71A",
        ["🔔"] = "\uE7ED",

        // Objects / sections
        ["📦"] = "\uE7B8",
        ["🗂"] = "\uE8B7",
        ["📅"] = "\uE787",
        ["⚙️"] = "\uE713", ["⚙"] = "\uE713",
        ["🛠"] = "\uE7C3",
        ["📝"] = "\uE70F",
        ["🏢"] = "\uE80F",
        ["🌐"] = "\uE774",
        ["🗄"] = "\uE7C9",
        ["💾"] = "\uE74E",
        ["🔒"] = "\uE72E",
        ["ℹ️"] = "\uE946", ["ℹ"] = "\uE946",
    };

    private static string? FindIconFont()
    {
        using var fonts = new System.Drawing.Text.InstalledFontCollection();
        foreach (var name in FontCandidates)
        {
            if (fonts.Families.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return name;
        }
        return null;
    }

    public static Font GetFont(float size)
    {
        if (size <= 0) size = 10f;
        var fontName = IconFontName ?? "Segoe UI Emoji";
        return new Font(fontName, size, FontStyle.Regular, GraphicsUnit.Point);
    }

    public static string ResolveIcon(string icon)
    {
        if (string.IsNullOrEmpty(icon)) return icon;
        if (!HasIconFont) return icon;
        if (LegacyMap.TryGetValue(icon, out var mapped)) return mapped;
        if (IsPrivateUse(icon)) return icon;
        return icon;
    }

    public static bool TrySplitLeadingIcon(string text, out string icon, out string rest)
    {
        icon = "";
        rest = text;
        if (!HasIconFont || string.IsNullOrWhiteSpace(text)) return false;

        var e = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        if (!e.MoveNext()) return false;
        var first = e.GetTextElement();
        if (!LegacyMap.ContainsKey(first) && !IsPrivateUse(first)) return false;

        icon = first;
        rest = text.Substring(first.Length).TrimStart();
        return true;
    }

    public static string StripLeadingIcon(string text)
    {
        return TrySplitLeadingIcon(text, out _, out var rest) ? rest : text;
    }

    public static void EnableButtonIcon(Button btn, string originalText)
    {
        if (btn == null) return;
        if (!ButtonInfos.TryGetValue(btn, out var info))
        {
            info = new ButtonIconInfo { OriginalText = originalText };
            ButtonInfos.Add(btn, info);
        }
        else
        {
            info.OriginalText = originalText;
        }

        if (!info.EventsHooked)
        {
            info.EventsHooked = true;
            btn.ForeColorChanged += (_, _) => RefreshButtonIcon(btn);
            btn.SizeChanged += (_, _) => RefreshButtonIcon(btn);
        }

        RefreshButtonIcon(btn);
    }

    private static void RefreshButtonIcon(Button btn)
    {
        if (!ButtonInfos.TryGetValue(btn, out var info)) return;
        bool smallButton = btn.Width < 110 || btn.Height < 30;

        if (!HasIconFont || smallButton)
        {
            btn.Image = null;
            btn.Text = info.OriginalText;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.ImageAlign = ContentAlignment.MiddleCenter;
            btn.TextImageRelation = TextImageRelation.Overlay;
            btn.Padding = new Padding(2, 0, 2, 0);
            return;
        }

        if (!TrySplitLeadingIcon(info.OriginalText, out var icon, out var rest))
        {
            btn.Image = null;
            btn.Text = info.OriginalText;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.ImageAlign = ContentAlignment.MiddleCenter;
            btn.TextImageRelation = TextImageRelation.Overlay;
            btn.Padding = new Padding(2, 0, 2, 0);
            return;
        }

        var glyph = ResolveIcon(icon);
        var iconPx = Math.Max(12, (int)Math.Round(btn.Height * 0.52));
        btn.Image = RenderIcon(glyph, btn.ForeColor, iconPx);
        btn.ImageAlign = ContentAlignment.MiddleLeft;
        btn.TextAlign = ContentAlignment.MiddleLeft;
        btn.TextImageRelation = TextImageRelation.ImageBeforeText;
        btn.Padding = new Padding(Math.Max(8, iconPx / 2), 0, 8, 0);
        btn.Text = rest;
    }

    private static Image? RenderIcon(string icon, Color color, int size)
    {
        if (string.IsNullOrEmpty(icon)) return null;
        var key = $"{icon}:{size}:{color.ToArgb()}";
        if (ImageCache.TryGetValue(key, out var img)) return img;

        var bmp = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var f = GetFont(size * 0.85f);
        using var br = new SolidBrush(color);
        var rect = new RectangleF(0, 0, size, size);
        using var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoClip
        };
        g.DrawString(icon, f, br, rect, sf);

        ImageCache[key] = bmp;
        return bmp;
    }

    private static bool IsPrivateUse(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        int code = char.ConvertToUtf32(s, 0);
        return (code >= 0xE000 && code <= 0xF8FF) ||
               (code >= 0xF0000 && code <= 0xFFFFD) ||
               (code >= 0x100000 && code <= 0x10FFFD);
    }
}
