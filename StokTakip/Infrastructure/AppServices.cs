using StokTakip.Data;
using StokTakip.Data.Interfaces;
using StokTakip.Services;

namespace StokTakip.Infrastructure;

/// <summary>
/// Uygulama genelinde paylaşılan bağlam.
/// Sadece Settings ve Session gibi uygulama ömrü boyunca yaşayan durumları yönetir.
/// </summary>
public sealed class AppServices : IDisposable
{
    private static AppServices? _instance;
    private bool _disposed;

    public static AppServices Current => _instance
        ?? throw new InvalidOperationException("AppServices henüz başlatılmadı.");

    public AppSettings Settings { get; private set; }
    public Database Database { get; private set; }
    public UserSession? Session { get; private set; }

    public bool IsAuthenticated => Session != null;

    public AppServices(AppSettings settings, Database database)
    {
        Settings = settings;
        Database = database;
        _instance = this;
    }

    public void BeginSession(string username, string role = "admin")
    {
        Session = UserSession.Create(username, role);
    }

    public void EndSession()
    {
        Session = null;
    }

    public void UpdateSettings(AppSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Database?.Dispose();
        _instance = null;
    }
}
