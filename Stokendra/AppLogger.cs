namespace Stokendra;

/// <summary>
/// Uygulama genelinde hata ve bilgi günlüğü yönetimi.
/// Günlük dosyası: <c>AppData/Local/Stokendra/logs/error.log</c>
/// Aç-yaz-kapat yöntemi kullanarak çökme durumlarında logların anında diskte kaydedilmesini garanti eder.
/// Log rotation: 5MB üzerinde eski log arşivlenir.
/// </summary>
public static class AppLogger
{
    /// <summary>Hata günlüğü dosyasının tam yolu.</summary>
    public static string ErrorLogPath => Path.Combine(
        AppPaths.EnsureDirectory(Path.Combine(AppPaths.ApplicationDataDirectory, "logs")),
        "error.log");

    private static readonly object _lock = new();
    private const long MaxLogSizeBytes = 5 * 1024 * 1024; // 5 MB

    /// <summary>
    /// Belirtilen seviyede günlük girdisini dosyaya yazar.
    /// Dosya 5MB'ı aşarsa otomatik olarak arşivlenir.
    /// </summary>
    public static void Log(string level, string message, Exception? ex = null)
    {
        lock (_lock)
        {
            try
            {
                string logPath = ErrorLogPath;
                RotateIfNeeded(logPath);

                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level.ToUpperInvariant()}: {message}";
                if (ex != null)
                    logEntry += $"\r\nException: {ex}";
                logEntry += "\r\n";

                using var writer = new StreamWriter(logPath, append: true, System.Text.Encoding.UTF8);
                writer.Write(logEntry);

                System.Diagnostics.Debug.WriteLine(logEntry);
            }
            catch
            {
                // Günlük yazma başarısız olursa sessizce geç
            }
        }
    }

    /// <summary>
    /// Hata mesajını ve opsiyonel exception'ı günlük dosyasına yazar.
    /// </summary>
    public static void LogError(string message, Exception? ex = null)
    {
        Log("ERROR", message, ex);
    }

    /// <summary>
    /// Günlük dosyasından son belirtilen satır sayısını okur.
    /// </summary>
    public static string GetRecentLogs(int maxLines = 50)
    {
        lock (_lock)
        {
            try
            {
                string logPath = ErrorLogPath;
                if (!File.Exists(logPath))
                    return "Kayıtlı hata günlüğü bulunmuyor.";

                var lines = File.ReadAllLines(logPath);
                if (lines.Length == 0)
                    return "Hata günlüğü boş.";

                int take = Math.Min(lines.Length, maxLines);
                return string.Join(Environment.NewLine, lines.Skip(lines.Length - take));
            }
            catch (Exception ex)
            {
                return $"Log okuma hatası: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Tanılama ve hata bildirimi için sistem özet bilgilerini üretir.
    /// </summary>
    public static string GetSystemDiagnosticSummary()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Uygulama: Stokendra v{AppInfo.VersionString}");
        sb.AppendLine($"İşletim Sistemi: {Environment.OSVersion.VersionString} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})");
        sb.AppendLine($".NET Çalışma Zamanı: {Environment.Version}");
        sb.AppendLine($"Makine Adı: {Environment.MachineName}");
        sb.AppendLine($"Tarih/Saat: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        return sb.ToString();
    }

    /// <summary>
    /// Log dosyası belirtilen boyutu aşarsa tarih damgalı arşiv dosyasına taşır.
    /// </summary>
    private static void RotateIfNeeded(string logPath)
    {
        try
        {
            if (!File.Exists(logPath)) return;
            var info = new FileInfo(logPath);
            if (info.Length < MaxLogSizeBytes) return;

            string archivePath = logPath.Replace(".log", $"_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            File.Move(logPath, archivePath);

            // Eski arşivleri temizle (son 10 tane tut)
            string? logDir = Path.GetDirectoryName(logPath);
            if (logDir == null) return;

            var archives = Directory.GetFiles(logDir, "error_*.log")
                .OrderByDescending(f => f)
                .Skip(10)
                .ToArray();

            foreach (var old in archives)
            {
                try { File.Delete(old); } catch { }
            }
        }
        catch
        {
            // Rotation başarısız olursa mevcut dosyaya yazmaya devam et
        }
    }
}
