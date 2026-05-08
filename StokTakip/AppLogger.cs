namespace StokTakip;

/// <summary>
/// Uygulama genelinde hata ve bilgi günlüğü yönetimi.
/// Günlük dosyası: <c>AppData/Local/Stokendra/logs/error.log</c>
/// </summary>
public static class AppLogger
{
    /// <summary>Hata günlüğü dosyasının tam yolu.</summary>
    public static string ErrorLogPath => Path.Combine(
        AppPaths.EnsureDirectory(Path.Combine(AppPaths.ApplicationDataDirectory, "logs")),
        "error.log");

    private static readonly object _lock = new();

    /// <summary>
    /// Hata mesajını ve opsiyonel exception'ı günlük dosyasına yazar.
    /// </summary>
    public static void LogError(string message, Exception? ex = null)
    {
        lock (_lock)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}";
                if (ex != null)
                    logEntry += $"\r\nException: {ex}";

                File.AppendAllText(ErrorLogPath, logEntry + "\r\n");
                System.Diagnostics.Debug.WriteLine(logEntry);
            }
            catch
            {
                // Günlük yazma başarısız olursa sessizce geç
            }
        }
    }
}
