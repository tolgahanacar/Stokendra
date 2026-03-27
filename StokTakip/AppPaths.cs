namespace StokTakip;

public static class AppPaths
{
    private static readonly string AppDataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Stokendra");

    public static string ApplicationDataDirectory => EnsureDirectory(AppDataRoot);
    public static string SettingsFilePath => Path.Combine(ApplicationDataDirectory, "ayarlar.json");
    public static string CrashLogPath => Path.Combine(ApplicationDataDirectory, "crash_log.txt");
    public static string DefaultDatabasePath => Path.Combine(ApplicationDataDirectory, "stok.db");

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

    public static string EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        return path;
    }
}
