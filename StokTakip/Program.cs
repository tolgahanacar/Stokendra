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
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        ApplicationConfiguration.Initialize();

        Settings = AppSettings.Yukle();
        LocalizationManager.Initialize(Settings.Language);

        if (string.IsNullOrWhiteSpace(Settings.DbPath) || !File.Exists(Settings.DbPath))
        {
            using var setup = new DbPathForm();
            if (setup.ShowDialog() != DialogResult.OK)
                return;
            Settings.DbPath = setup.SecilenYol;
            Settings.Kaydet();
        }

        DB = new Data.Database(Settings.DbPath);

        // Login gate
        using var login = new LoginForm();
        if (login.ShowDialog() != DialogResult.OK)
            return;

        Application.Run(new MainForm());
    }
}
