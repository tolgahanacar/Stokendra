using System.Text.Json;

namespace Stokendra;

/// <summary>
/// JSON tabanli coklu dil yonetim sistemi.
/// L("key") kisayoluyla kullanilir.
/// </summary>
public static class LocalizationManager
{
    private static readonly Dictionary<string, Dictionary<string, string>> BuiltInFallbacks = new()
    {
        ["tr"] = new Dictionary<string, string>
        {
            ["bulk_edit_partial_error"] = "{0} kayıt güncellendi, ancak bir hata oluştu:\n{1}",
            ["movement_type_invalid"] = "Geçersiz hareket türü.",
            ["movement_not_found"] = "Stok hareketi bulunamadı.",
            ["movement_only_child_cards"] = "Stok hareketleri yalnızca alt kartlar için uygulanabilir.",
            ["stock_would_go_negative"] = "Bu işlem, stok sonucunu negatife düşüreceği için uygulanamaz.",
            ["record_not_found"] = "Kayıt bulunamadı.",
            ["bulk_operation_empty"] = "İşlem yapılacak kayıt bulunamadı.",
            ["config_key_required"] = "Ayar anahtarı boş olamaz.",
            ["db_integrity_failed"] = "Veritabanı bütünlük kontrolü başarısız oldu: {0}",
            ["stock_code_exists"] = "Bu stok kodu zaten kullanılıyor.",
            ["name_required"] = "Stok adı zorunludur.",
            ["code_required"] = "Stok kodu zorunludur.",
            ["parent_card_self"] = "Bir kart kendisine üst kart olarak bağlanamaz.",
            ["parent_card_not_found"] = "Üst kart bulunamadı.",
            ["parent_card_with_movements"] = "Hareket geçmişi olan bir kart üst kart olarak düzenlenemez.",
            ["parent_card_loop"] = "Üst kart ilişkisi döngü oluşturuyor.",
            ["error_with_details"] = "Bir hata oluştu:\n{0}",
            ["password_min_length"] = "Şifre en az 4 karakter olmalıdır.",
            ["password_contains_username"] = "Şifre kullanıcı adını içeremez.",
            ["low"] = "Düşük",
            ["loading"] = "Yükleniyor...",
            ["status"] = "Durum",
            ["today_movements_sub"] = "Bugünkü giriş/çıkışlar",
            ["grand_total"] = "Genel Toplam:",
            ["created"] = "Oluşturma:",
            ["updated"] = "Güncelleme:",
            ["title"] = "Başlık",
            ["content"] = "İçerik",
            ["entry_symbol"] = "[G]",
            ["exit_symbol"] = "[Ç]"
        },
        ["en"] = new Dictionary<string, string>
        {
            ["bulk_edit_partial_error"] = "{0} records were updated, but an error occurred:\n{1}",
            ["movement_type_invalid"] = "Invalid movement type.",
            ["movement_not_found"] = "Stock movement not found.",
            ["movement_only_child_cards"] = "Stock movements can only be applied to child cards.",
            ["stock_would_go_negative"] = "This action cannot be completed because it would make the resulting stock negative.",
            ["record_not_found"] = "Record not found.",
            ["bulk_operation_empty"] = "There are no records to process.",
            ["config_key_required"] = "Configuration key cannot be empty.",
            ["db_integrity_failed"] = "Database integrity check failed: {0}",
            ["stock_code_exists"] = "This stock code is already in use.",
            ["name_required"] = "Stock name is required.",
            ["code_required"] = "Stock code is required.",
            ["parent_card_self"] = "A card cannot be assigned as its own parent.",
            ["parent_card_not_found"] = "Parent card not found.",
            ["parent_card_with_movements"] = "A card with movement history cannot be changed into a parent card.",
            ["parent_card_loop"] = "The parent card relationship creates a loop.",
            ["error_with_details"] = "An error occurred:\n{0}",
            ["password_min_length"] = "Password must be at least 4 characters.",
            ["password_contains_username"] = "Password cannot contain the username.",
            ["low"] = "Low",
            ["loading"] = "Loading...",
            ["status"] = "Status",
            ["today_movements_sub"] = "Today's entries/exits",
            ["grand_total"] = "Grand Total:",
            ["created"] = "Created:",
            ["updated"] = "Updated:",
            ["title"] = "Title",
            ["content"] = "Content",
            ["entry_symbol"] = "[E]",
            ["exit_symbol"] = "[X]"
        }
    };

    private static Dictionary<string, string> _strings = new();
    private static string _currentLang = "tr";
    private static readonly object _initLock = new();

    public static string CurrentLanguage => _currentLang;

    public static string[] SupportedLanguages => new[] { "tr", "en" };
    public static string[] LanguageDisplayNames => new[] { "Türkçe", "English" };

    public static void Initialize(string language = "tr")
    {
        lock (_initLock)
        {
            _currentLang = language;
            LoadLanguage(language);
        }
    }

    private static void LoadLanguage(string lang)
    {
        // lock(_initLock) çağıran tarafından zaten tutulmuş durumda
        var newStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string resDir = Path.Combine(baseDir, "Resources");
        string filePath = Path.Combine(resDir, $"lang_{lang}.json");

        if (!File.Exists(filePath))
            filePath = Path.Combine(baseDir, $"lang_{lang}.json");

        if (!File.Exists(filePath))
        {
            string? procPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(procPath))
            {
                string procDir = Path.GetDirectoryName(procPath)!;
                filePath = Path.Combine(procDir, "Resources", $"lang_{lang}.json");
                if (!File.Exists(filePath))
                    filePath = Path.Combine(procDir, $"lang_{lang}.json");
            }
        }

        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                newStrings = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Localization load error for " + filePath + ": " + ex);
                newStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        // Atomic assignment — okuyucular eski referansı görmeye devam edebilir ama
        // yeni referans atandıktan sonra tutarlı bir snapshot görürler.
        System.Threading.Volatile.Write(ref _strings, newStrings);
    }

    public static string L(string key)
    {
        return TryGetText(key, out var value) ? value : key;
    }

    public static string L(string key, params object[] args)
    {
        var template = TryGetText(key, out var value) ? value : key;
        try { return string.Format(template, args); }
        catch { return template; }
    }

    private static bool TryGetText(string key, out string value)
    {
        // Volatile.Read ile thread-safe snapshot al
        var strings = System.Threading.Volatile.Read(ref _strings);
        if (strings.TryGetValue(key, out value!))
            return true;

        return BuiltInFallbacks.TryGetValue(_currentLang, out var fallback)
            && fallback.TryGetValue(key, out value!);
    }
}
