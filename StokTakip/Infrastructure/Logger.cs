using System;

namespace StokTakip.Infrastructure;

public class Logger : ILogger
{
    public void LogInfo(string message)
    {
        // For now, reuse AppLogger or just Debug.WriteLine
        System.Diagnostics.Debug.WriteLine($"INFO: {message}");
    }

    public void LogWarning(string message)
    {
        System.Diagnostics.Debug.WriteLine($"WARN: {message}");
    }

    public void LogError(string message, Exception? ex = null)
    {
        AppLogger.LogError(message, ex);
    }
}
