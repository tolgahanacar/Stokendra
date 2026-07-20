namespace Stokendra;

/// <summary>
/// Uygulama dosya yolları ve dizin yönetimi için merkezi yardımcı sınıf.
/// Tüm yollar <c>AppData/Local/Stokendra/</c> altında normalize edilir.
/// </summary>
public static class AppPaths
{
    public static string? TestDataRootOverride { get; set; }

    private static string AppDataRoot => TestDataRootOverride ?? Path.Combine(
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

    /// <summary>
    /// Migrates database and settings from legacy StokTakip folder to new Stokendra folder if necessary.
    /// </summary>
    public static void MigrateLegacyData()
    {
        try
        {
            string oldRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StokTakip");
            string newRoot = ApplicationDataDirectory;

            if (Directory.Exists(oldRoot))
            {
                string oldSettings = Path.Combine(oldRoot, "ayarlar.json");
                string newSettings = SettingsFilePath;

                if (File.Exists(oldSettings) && !File.Exists(newSettings))
                {
                    File.Copy(oldSettings, newSettings, true);
                }

                string oldDb = Path.Combine(oldRoot, "stok.db");
                string newDb = DefaultDatabasePath;

                if (File.Exists(oldDb))
                {
                    bool shouldCopy = false;
                    if (!File.Exists(newDb))
                    {
                        shouldCopy = true;
                    }
                    else
                    {
                        // Check if the new database is empty (no stock cards)
                        try
                        {
                            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={newDb}");
                            conn.Open();
                            using var cmd = conn.CreateCommand();
                            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('StockCards', 'StokKartlari')";
                            var tableCount = Convert.ToInt32(cmd.ExecuteScalar());
                            if (tableCount == 0)
                            {
                                shouldCopy = true;
                            }
                            else
                            {
                                cmd.CommandText = "SELECT COUNT(*) FROM StockCards";
                                try
                                {
                                    var cardCount = Convert.ToInt32(cmd.ExecuteScalar());
                                    if (cardCount == 0) shouldCopy = true;
                                }
                                catch
                                {
                                    cmd.CommandText = "SELECT COUNT(*) FROM StokKartlari";
                                    var cardCount = Convert.ToInt32(cmd.ExecuteScalar());
                                    if (cardCount == 0) shouldCopy = true;
                                }
                            }
                        }
                        catch
                        {
                            shouldCopy = true;
                        }
                    }

                    if (shouldCopy)
                    {
                        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                        if (File.Exists(newDb))
                        {
                            string backupPath = newDb + ".backup";
                            if (File.Exists(backupPath)) File.Delete(backupPath);
                            File.Move(newDb, backupPath);
                        }
                        File.Copy(oldDb, newDb, true);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Legacy migration error: {ex.Message}");
        }
    }
}
