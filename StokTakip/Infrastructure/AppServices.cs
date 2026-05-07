using StokTakip.Data;
using StokTakip.Data.Interfaces;

namespace StokTakip.Infrastructure;

/// <summary>
/// Uygulama genelinde paylaşılan bağlam. Global static state'i kapsüller.
/// Tüm erişim bu sınıf üzerinden yapılır; <c>Program.DB!</c> doğrudan kullanımı kaldırılmıştır.
/// </summary>
public sealed class AppServices : IDisposable
{
    private static AppServices? _instance;
    private bool _disposed;

    /// <summary>Singleton uygulama bağlamı.</summary>
    public static AppServices Current => _instance
        ?? throw new InvalidOperationException(
            "AppServices henüz başlatılmadı. Initialize() çağrılmalıdır.");

    /// <summary>Aktif IoC container.</summary>
    public ServiceContainer Services { get; }

    /// <summary>Yüklü uygulama ayarları.</summary>
    public AppSettings Settings { get; private set; }

    /// <summary>Aktif veritabanı bağlantısı.</summary>
    public Database Database { get; private set; }

    /// <summary>Aktif kullanıcı oturumu. Login öncesinde null.</summary>
    public UserSession? Session { get; private set; }

    /// <summary>Oturum açık mı?</summary>
    public bool IsAuthenticated => Session != null;

    // ── Repository kısayolları (IoC üzerinden) ────────────────────────────

    /// <summary>Stok kartı repository'si.</summary>
    public IStockCardRepository StockCards => Services.Resolve<IStockCardRepository>();

    /// <summary>Stok hareketi repository'si.</summary>
    public IMovementRepository Movements => Services.Resolve<IMovementRepository>();

    /// <summary>Servis kaydı repository'si.</summary>
    public IServiceRecordRepository ServiceRecords => Services.Resolve<IServiceRecordRepository>();

    /// <summary>Not repository'si.</summary>
    public INoteRepository Notes => Services.Resolve<INoteRepository>();

    /// <summary>Departman repository'si.</summary>
    public IDepartmentRepository Departments => Services.Resolve<IDepartmentRepository>();

    /// <summary>Rapor repository'si.</summary>
    public IReportRepository Reports => Services.Resolve<IReportRepository>();

    /// <summary>Kullanıcı repository'si.</summary>
    public IUserRepository Users => Services.Resolve<IUserRepository>();

    /// <summary>Yapılandırma repository'si.</summary>
    public IConfigRepository Config => Services.Resolve<IConfigRepository>();

    private AppServices(ServiceContainer services, AppSettings settings, Database database)
    {
        Services = services;
        Settings = settings;
        Database = database;
    }

    /// <summary>
    /// Uygulama bağlamını başlatır. Yalnızca bir kez çağrılmalıdır.
    /// </summary>
    public static AppServices Initialize(
        ServiceContainer services,
        AppSettings settings,
        Database database)
    {
        _instance = new AppServices(services, settings, database);
        return _instance;
    }

    /// <summary>
    /// Mevcut oturumu başlatır. Başarılı login sonrasında çağrılır.
    /// </summary>
    public void BeginSession(string username, string role = "admin")
    {
        Session = UserSession.Create(username, role);
    }

    /// <summary>
    /// Oturumu sonlandırır.
    /// </summary>
    public void EndSession()
    {
        Session = null;
    }

    /// <summary>
    /// Ayarları günceller (dil değişikliği vb. sonrasında).
    /// </summary>
    public void UpdateSettings(AppSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Database.Dispose();
        Services.Dispose();
        _instance = null;
    }
}
