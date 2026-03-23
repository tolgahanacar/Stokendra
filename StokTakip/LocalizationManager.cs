using System.Text.Json;

namespace StokTakip;

/// <summary>
/// JSON tabanlı çoklu dil yönetim sistemi.
/// L("key") kısayoluyla kullanılır.
/// </summary>
public static class LocalizationManager
{
    private static Dictionary<string, string> _strings = new();
    private static string _currentLang = "tr";

    public static string CurrentLanguage => _currentLang;

    public static string[] SupportedLanguages => new[] { "tr", "en" };
    public static string[] LanguageDisplayNames => new[] { "Türkçe", "English" };

    public static void Initialize(string language = "tr")
    {
        _currentLang = language;
        LoadLanguage(language);
    }

    private static void LoadLanguage(string lang)
    {
        _strings.Clear();
        string baseDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        string resDir = Path.Combine(baseDir, "Resources");
        string filePath = Path.Combine(resDir, $"lang_{lang}.json");

        if (!File.Exists(filePath))
        {
            // Fallback: kaynak dosya Resources klasöründe yoksa, uygulama dizininde ara
            filePath = Path.Combine(baseDir, $"lang_{lang}.json");
        }

        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                    ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Localization load error for " + filePath + ": " + ex);
                _strings = new Dictionary<string, string>();
            }
        }
    }

    /// <summary>
    /// Lokalize edilmiş metni döndürür. Anahtar bulunamazsa anahtar kendisi döner.
    /// </summary>
    public static string L(string key)
    {
        return _strings.TryGetValue(key, out var value) ? value : key;
    }

    /// <summary>
    /// Formatlı lokalize metin: L("key", arg1, arg2)
    /// Dil dosyasında {0}, {1} yer tutucuları kullanılır.
    /// </summary>
    public static string L(string key, params object[] args)
    {
        var template = _strings.TryGetValue(key, out var value) ? value : key;
        try { return string.Format(template, args); }
        catch { return template; }
    }
}
