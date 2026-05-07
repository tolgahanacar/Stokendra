using StokTakip.Data;
using StokTakip.Data.Interfaces;
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

        Application.ThreadException += (_, e) => LogError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) LogError(ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogError(e.Exception);
            e.SetObserved();
        };

        var settings = AppSettings.Yukle();
        LocalizationManager.Initialize(settings.Language);

        if (!EnsureDatabasePath(settings))
            return;

        var db = TryInitializeDatabase(settings);
        if (db == null)
            return;

        // AppServices başlat — artık tüm global state buradan
        var services = new ServiceContainer();
        services
            .RegisterInstance<IStockCardRepository>(db)
            .RegisterInstance<IMovementRepository>(db)
            .RegisterInstance<IServiceRecordRepository>(db)
            .RegisterInstance<INoteRepository>(db)
            .RegisterInstance<IDepartmentRepository>(db)
            .RegisterInstance<IReportRepository>(db)
            .RegisterInstance<IUserRepository>(db)
            .RegisterInstance<IConfigRepository>(db)
            .RegisterSingleton<IStockCardService, StockCardService>(() => new StockCardService(db))
            .RegisterSingleton<IMovementService, MovementService>(() => new MovementService(db, db));

        var ctx = AppServices.Initialize(services, settings, db);

        Application.ApplicationExit += (_, _) => ctx.Dispose();

        using var login = new LoginForm();
        if (login.ShowDialog() != DialogResult.OK)
            return;

        Application.Run(new MainForm());
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
