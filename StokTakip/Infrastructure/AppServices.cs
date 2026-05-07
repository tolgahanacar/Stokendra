using StokTakip.Data;
using StokTakip.Data.Interfaces;
using StokTakip.Services;

namespace StokTakip.Infrastructure;

/// <summary>
/// Uygulama genelinde paylaşılan bağlam.
/// Sadece Settings ve Session gibi uygulama ömrü boyunca yaşayan durumları yönetir.
/// Servislere ve Repository'lere erişim için Dependency Injection tercih edilmelidir.
/// </summary>
public sealed class AppServices : IDisposable
{
    private static AppServices? _instance;
    private bool _disposed;

    public static AppServices Current => _instance
        ?? throw new InvalidOperationException("AppServices henüz başlatılmadı.");

    public ServiceContainer Services { get; }
    public AppSettings Settings { get; private set; }
    public Database Database { get; private set; }
    public UserSession? Session { get; private set; }

    public bool IsAuthenticated => Session != null;

    // ── Repository shortcuts (resolving from container) ──────────────────

    public IStockCardRepository StockCards => Services.Resolve<IStockCardRepository>();
    public IMovementRepository Movements => Services.Resolve<IMovementRepository>();
    public IUserRepository Users => Services.Resolve<IUserRepository>();
    public INoteRepository Notes => Services.Resolve<INoteRepository>();
    public IReportRepository Reports => Services.Resolve<IReportRepository>();
    public IServiceRecordRepository ServiceRecords => Services.Resolve<IServiceRecordRepository>();
    public IDepartmentRepository Departments => Services.Resolve<IDepartmentRepository>();
    public IConfigRepository Config => Services.Resolve<IConfigRepository>();

    public IStockCardService StockCardService => Services.Resolve<IStockCardService>();
    public IMovementService MovementService => Services.Resolve<IMovementService>();

    private AppServices(ServiceContainer services, AppSettings settings, Database database)
    {
        Services = services;
        Settings = settings;
        Database = database;
    }

    public static AppServices Initialize(ServiceContainer services, AppSettings settings, Database database)
    {
        _instance = new AppServices(services, settings, database);
        return _instance;
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
        Services.Dispose();
        _instance = null;
    }
}

