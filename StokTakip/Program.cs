using StokTakip.Forms;
using StokTakip.Data;

namespace StokTakip;

static class Program
{
    public static Database? DB { get; private set; }
    public static AppSettings Settings { get; private set; } = new();
    public static string CurrentUser { get; set; } = "admin";

    [STAThread]
    static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        ApplicationConfiguration.Initialize();

        Application.ThreadException += (s, e) => {
            LogError(e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) => {
            if (e.ExceptionObject is Exception ex) LogError(ex);
        };
        TaskScheduler.UnobservedTaskException += (s, e) =>
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
            DB = new Data.Database(Settings.DbPath);
            Application.ApplicationExit += (s, e) => DB?.Dispose();
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

            DB = new Data.Database(Settings.DbPath);
            Application.ApplicationExit += (s, e) => DB?.Dispose();
            return true;
        }
    }

    private static void LogError(Exception ex)
    {
        try
        {
            _ = AppPaths.ApplicationDataDirectory;
            File.AppendAllText(
                AppPaths.CrashLogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
            MessageBox.Show("Kritik Hata: " + ex.Message + "\n\nLog: " + AppPaths.CrashLogPath, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch { }
    }
}
