using StokTakip.Data;
using StokTakip.Data.Interfaces;
using StokTakip.Data.Repositories;
using StokTakip.Forms;
using StokTakip.Infrastructure;
using StokTakip.Services;

namespace StokTakip;

/// <summary>
/// Uygulama giriş noktası. Servis kaydı, veritabanı başlatma ve form akışını yönetir.
/// Tüm global state <see cref="AppServices.Current"/> üzerinden erişilir.
/// </summary>
static class Program
{

    [STAThread]
    static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        ApplicationConfiguration.Initialize();

        // Exception handling logic ... (omitted for brevity, keep existing)
        SetupExceptionHandlers();

        var settings = AppSettings.Yukle();
        LocalizationManager.Initialize(settings.Language);

        if (!EnsureDatabasePath(settings)) return;

        // DI Container Setup
        var services = new ServiceContainer();
        ConfigureServices(services, settings);

        // Run migrations
        var migrator = services.Resolve<DatabaseMigrator>();
        try
        {
            migrator.Migrate();
        }
        catch (Exception ex)
        {
            LogError(ex);
            return;
        }

        var db = new Database(settings.DbPath);
        var ctx = AppServices.Initialize(services, settings, db);
        Application.ApplicationExit += (_, _) => ctx.Dispose();

        // Resolve LoginForm via DI
        using var login = services.Resolve<LoginForm>();
        if (login.ShowDialog() != DialogResult.OK) return;

        // Resolve MainForm via DI
        Application.Run(services.Resolve<MainForm>());
    }

    private static void ConfigureServices(ServiceContainer services, AppSettings settings)
    {
        // Infrastructure
        var dbFactory = new SqliteConnectionFactory(settings.DbPath);
        services.RegisterInstance<IDbConnectionFactory>(dbFactory);
        services.RegisterSingleton<DatabaseMigrator, DatabaseMigrator>();

        // Repositories
        services.RegisterSingleton<IStockCardRepository, StockCardRepository>();
        services.RegisterSingleton<IMovementRepository, MovementRepository>();
        services.RegisterSingleton<IUserRepository, UserRepository>();
        services.RegisterSingleton<INoteRepository, NoteRepository>();
        services.RegisterSingleton<IReportRepository, ReportRepository>();
        services.RegisterSingleton<IServiceRecordRepository, ServiceRecordRepository>();
        services.RegisterSingleton<IDepartmentRepository, DepartmentRepository>();
        services.RegisterSingleton<IConfigRepository, ConfigRepository>();

        // Forms (Explicitly register to ensure dependencies are injected correctly)
        services.RegisterTransient<LoginForm, LoginForm>();
        services.RegisterTransient<MainForm, MainForm>();
        services.RegisterTransient<DashboardPanel, DashboardPanel>();

        // Business Services
        services.RegisterSingleton<IStockCardService, StockCardService>();
        services.RegisterSingleton<IMovementService, MovementService>();

        // Forms (Auto-resolved if concrete, but can be explicit)
    }

    private static void SetupExceptionHandlers()
    {
        Application.ThreadException += (_, e) => LogError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception ex) LogError(ex); };
        TaskScheduler.UnobservedTaskException += (_, e) => { LogError(e.Exception); e.SetObserved(); };
    }

    private static bool EnsureDatabasePath(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.DbPath))
        {
            try
            {
                settings.DbPath = AppPaths.NormalizeDatabasePath(settings.DbPath);
                settings.Kaydet();
                return true;
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        using var setup = new DbPathForm();
        if (setup.ShowDialog() != DialogResult.OK)
            return false;

        settings.DbPath = setup.SecilenYol;
        settings.Kaydet();
        return true;
    }

    private static Database? TryInitializeDatabase(AppSettings settings)
    {
        try
        {
            return new Database(settings.DbPath);
        }
        catch (Exception ex)
        {
            LogError(ex);
            MessageBox.Show(
                "Veritabani acilamadi veya dogrulanamadi.\nLutfen yeni bir veritabani konumu secin.",
                "Hata",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        using var setup = new DbPathForm();
        if (setup.ShowDialog() != DialogResult.OK)
            return null;

        settings.DbPath = setup.SecilenYol;
        settings.Kaydet();

        try
        {
            return new Database(settings.DbPath);
        }
        catch (Exception retryEx)
        {
            LogError(retryEx);
            MessageBox.Show(
                "Yeni konum da açılamadı.\n" + retryEx.Message,
                "Kritik Hata",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return null;
        }
    }

    internal static void LogError(Exception ex)
    {
        try
        {
            _ = AppPaths.ApplicationDataDirectory;
            File.AppendAllText(
                AppPaths.CrashLogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { }

        try
        {
            if (Application.MessageLoop)
            {
                MessageBox.Show(
                    "Kritik Hata: " + ex.Message + "\n\nLog: " + AppPaths.CrashLogPath,
                    "Hata",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        catch { }
    }
}
