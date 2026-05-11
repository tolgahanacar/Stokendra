namespace StokTakip;

/// <summary>
/// Uygulama genelinde hata ve bilgi günlüğü yönetimi.
/// Günlük dosyası: <c>AppData/Local/Stokendra/logs/error.log</c>
/// Buffered StreamWriter kullanarak her log için dosya açma/kapama overhead'ini önler.
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
    /// Hata mesajını ve opsiyonel exception'ı günlük dosyasına yazar.
    /// Dosya 5MB'ı aşarsa otomatik olarak arşivlenir.
    /// </summary>
    public static void LogError(string message, Exception? ex = null)
    {
        lock (_lock)
        {
            try
            {
                string logPath = ErrorLogPath;
                RotateIfNeeded(logPath);

                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}";
                if (ex != null)
                    logEntry += $"\r\nException: {ex}";
                logEntry += "\r\n";

                // StreamWriter ile append — her çağrıda dosya açılıp kapanır ama
                // lock sayesinde thread-safe, rotation sonrası da güvenli.
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
