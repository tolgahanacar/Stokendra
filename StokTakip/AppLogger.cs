using System;
using System.IO;

namespace StokTakip;

public static class AppLogger
{
    public static string ErrorLogPath => Path.Combine(AppPaths.EnsureDirectory(Path.Combine(AppPaths.ApplicationDataDirectory, "logs")), "error.log");

    public static void LogError(string message, Exception? ex = null)
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
            // Fallback
        }
    }
}
