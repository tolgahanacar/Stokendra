using System;

namespace StokTakip.Infrastructure;

/// <summary>
/// ILogger implementasyonu. Tüm seviyeler AppLogger üzerinden dosyaya yazılır.
/// Debug/Trace seviyesi sadece Debug build'de Debug.WriteLine'a da gider.
/// </summary>
public class Logger : ILogger
{
    public void LogInfo(string message)
    {
        // Info seviyesi dosyaya yazılır — production'da Debug.WriteLine kaybolur
        AppLogger.LogError($"INFO: {message}");
        System.Diagnostics.Debug.WriteLine($"INFO: {message}");
    }

    public void LogWarning(string message)
    {
        AppLogger.LogError($"WARN: {message}");
        System.Diagnostics.Debug.WriteLine($"WARN: {message}");
    }

    public void LogError(string message, Exception? ex = null)
    {
        AppLogger.LogError(message, ex);
    }
}
