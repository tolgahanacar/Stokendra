using System;

namespace Stokendra.Infrastructure;

/// <summary>
/// ILogger implementasyonu. Tüm seviyeler AppLogger üzerinden dosyaya yazılır.
/// Debug/Trace seviyesi sadece Debug build'de Debug.WriteLine'a da gider.
/// </summary>
public class Logger : ILogger
{
    public void LogInfo(string message)
    {
        AppLogger.Log("INFO", message);
    }

    public void LogWarning(string message)
    {
        AppLogger.Log("WARN", message);
    }

    public void LogError(string message, Exception? ex = null)
    {
        AppLogger.Log("ERROR", message, ex);
    }
}
