using StokTakip.Data;
using StokTakip.Data.Interfaces;
using StokTakip.Forms;
using StokTakip.Infrastructure;

namespace StokTakip;

/// <summary>
/// Uygulama giriş noktası. Servis kaydı, veritabanı başlatma ve form akışını yönetir.
/// </summary>
static class Program
{
    /// <summary>Uygulama genelinde kullanılan IoC container.</summary>
    public static ServiceContainer Services { get; } = new();

    /// <summary>Aktif veritabanı bağlantısı. Login öncesinde <c>null</c> olabilir.</summary>
    public static Database? DB { get; private set; }

    /// <summary>Yüklü uygulama ayarları.</summary>
    public static AppSettings Settings { get; private set; } = new();

    /// <summary>Oturum açmış kullanıcı adı.</summary>
    public static string CurrentUser { get; set; } = "admin";

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

        Settings = AppSettings.Yukle();
        LocalizationManager.Initialize(Settings.Language);

        if (!EnsureDatabasePath())
            return;

        if (!InitializeDatabase())
            return;

        using var login = new LoginForm();
        if (login.ShowDialog() != DialogResult.OK)
            return;

        Application.Run(new MainForm());
    }

    /// <summary>
    /// Tüm servisleri IoC container'a kaydeder.
    /// Veritabanı başlatıldıktan sonra çağrılmalıdır.
    /// </summary>
    private static void ConfigureServices()
    {
        if (DB == null) return;

        Services
            .RegisterInstance<IStockCardRepository>(DB)
            .RegisterInstance<IMovementRepository>(DB)
            .RegisterInstance<IServiceRecordRepository>(DB)
            .RegisterInstance<INoteRepository>(DB)
            .RegisterInstance<IDepartmentRepository>(DB)
            .RegisterInstance<IReportRepository>(DB)
            .RegisterInstance<IUserRepository>(DB)
            .RegisterInstance<IConfigRepository>(DB);
    }

    private static bool EnsureDatabasePath()
    {
        if (!string.IsNullOrWhiteSpace(Settings.DbPath))
        {
            try
            {
                Settings.DbPath = AppPaths.NormalizeDatabasePath(Settings.DbPath);
                Settings.Kaydet();
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

        Settings.DbPath = setup.SecilenYol;
        Settings.Kaydet();
        return true;
    }

    private static bool InitializeDatabase()
    {
        try
        {
            DB = new Database(Settings.DbPath);
            ConfigureServices();
            Application.ApplicationExit += (_, _) =>
            {
                DB?.Dispose();
                Services.Dispose();
            };
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex);
            MessageBox.Show(
                "Veritabani acilamadi veya dogrulanamadi.\nLutfen yeni bir veritabani konumu secin.",
                "Hata",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            using var setup = new DbPathForm();
            if (setup.ShowDialog() != DialogResult.OK)
                return false;

            Settings.DbPath = setup.SecilenYol;
            Settings.Kaydet();

            try
            {
                DB = new Database(Settings.DbPath);
                ConfigureServices();
                Application.ApplicationExit += (_, _) =>
                {
                    DB?.Dispose();
                    Services.Dispose();
                };
                return true;
            }
            catch (Exception retryEx)
            {
                LogError(retryEx);
                MessageBox.Show(
                    "Yeni konum da açılamadı.\n" + retryEx.Message,
                    "Kritik Hata",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }
    }

    private static void LogError(Exception ex)
    {
        try
        {
            // Crash log'a yaz (AppLogger'dan ayrı — uygulama başlamadan önce de çalışmalı)
            _ = AppPaths.ApplicationDataDirectory;
            File.AppendAllText(
                AppPaths.CrashLogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { }

        // MessageBox sadece ana thread'den (UI thread) gelen kritik hatalarda göster.
        // Arka plan thread'lerinden gelen hatalar (TaskScheduler, AppDomain) sessizce loglanır.
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
