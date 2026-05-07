namespace StokTakip;

/// <summary>
/// Uygulama dosya yolları ve dizin yönetimi için merkezi yardımcı sınıf.
/// Tüm yollar <c>AppData/Local/Stokendra/</c> altında normalize edilir.
/// </summary>
public static class AppPaths
{
    private static readonly string AppDataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Stokendra");

    /// <summary>Uygulama veri dizini. Yoksa otomatik oluşturulur.</summary>
    public static string ApplicationDataDirectory => EnsureDirectory(AppDataRoot);

    /// <summary>Ayarlar dosyasının tam yolu.</summary>
    public static string SettingsFilePath => Path.Combine(ApplicationDataDirectory, "ayarlar.json");

    /// <summary>Kritik hata günlüğü dosyasının tam yolu.</summary>
    public static string CrashLogPath => Path.Combine(ApplicationDataDirectory, "crash_log.txt");

    /// <summary>Varsayılan veritabanı dosyasının tam yolu.</summary>
    public static string DefaultDatabasePath => Path.Combine(ApplicationDataDirectory, "stok.db");

    /// <summary>Kullanıcı aktivite günlüğü dosyasının tam yolu.</summary>
    public static string ActivityLogPath => Path.Combine(
        EnsureDirectory(Path.Combine(ApplicationDataDirectory, "logs")),
        "userActivity.txt");

    /// <summary>
    /// Kullanıcı aktivitesini günlük dosyasına yazar.
    /// </summary>
    /// <param name="action">Gerçekleştirilen işlem (örn. "ekle", "sil").</param>
    /// <param name="module">İşlemin yapıldığı modül (örn. "stokkartlari").</param>
    /// <param name="details">İşlem detayı.</param>
    public static void LogActivity(string action, string module, string details)
    {
        try
        {
            // Tab-separated format: ayrıştırılabilir, detay içindeki özel karakterlerden etkilenmez
            string logLine = string.Join("\t",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                action?.Replace("\t", " ") ?? "",
                module?.Replace("\t", " ") ?? "",
                details?.Replace("\t", " ").Replace("\r", "").Replace("\n", " ") ?? "");
            File.AppendAllText(ActivityLogPath, logLine + Environment.NewLine);
        }
        catch { }
    }

    /// <summary>
    /// Veritabanı yolunu normalize eder. Göreceli yolları uygulama veri dizinine göre çözümler.
    /// Hedef dizin yoksa otomatik oluşturulur.
    /// </summary>
    /// <param name="dbPath">Ham veritabanı yolu. Boşsa varsayılan yol kullanılır.</param>
    /// <returns>Normalize edilmiş tam yol.</returns>
    /// <exception cref="InvalidOperationException">Dizin çözümlenemezse fırlatılır.</exception>
    public static string NormalizeDatabasePath(string? dbPath)
    {
        string candidate = string.IsNullOrWhiteSpace(dbPath) ? DefaultDatabasePath : dbPath.Trim();
        candidate = Environment.ExpandEnvironmentVariables(candidate);

        if (!Path.IsPathRooted(candidate))
            candidate = Path.GetFullPath(Path.Combine(ApplicationDataDirectory, candidate));
        else
            candidate = Path.GetFullPath(candidate);

        string? directory = Path.GetDirectoryName(candidate);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Veritabani klasoru cozumlenemedi.");

        EnsureDirectory(directory);
        return candidate;
    }

    /// <summary>
    /// Yazılabilir dosya yolunu normalize eder. Göreceli yolları uygulama veri dizinine göre çözümler.
    /// </summary>
    /// <param name="filePath">Ham dosya yolu.</param>
    /// <returns>Normalize edilmiş tam yol.</returns>
    /// <exception cref="InvalidOperationException">Yol boşsa veya çözümlenemezse fırlatılır.</exception>
    public static string NormalizeWritableFilePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new InvalidOperationException("Dosya yolu bos olamaz.");

        string normalized = Environment.ExpandEnvironmentVariables(filePath.Trim());
        normalized = Path.IsPathRooted(normalized)
            ? Path.GetFullPath(normalized)
            : Path.GetFullPath(Path.Combine(ApplicationDataDirectory, normalized));

        string? directory = Path.GetDirectoryName(normalized);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Dosya klasoru cozumlenemedi.");

        EnsureDirectory(directory);
        return normalized;
    }

    /// <summary>
    /// Belirtilen dizinin var olduğunu garantiler. Yoksa oluşturur.
    /// </summary>
    /// <param name="path">Kontrol edilecek dizin yolu.</param>
    /// <returns>Aynı dizin yolunu döner.</returns>
    public static string EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        return path;
    }
}
