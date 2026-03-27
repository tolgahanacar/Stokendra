using System.Text.Json;

namespace StokTakip;

public class AppSettings
{
    public string DbPath { get; set; } = "";
    public string Language { get; set; } = "tr";
    public string CompanyName { get; set; } = "";
    public string Theme { get; set; } = "dark";

    static AppSettings()
    {
        _ = AppPaths.ApplicationDataDirectory;
    }

    public static AppSettings Yukle()
    {
        if (File.Exists(AppPaths.SettingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(AppPaths.SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                settings.DbPath = string.IsNullOrWhiteSpace(settings.DbPath)
                    ? ""
                    : AppPaths.NormalizeDatabasePath(settings.DbPath);
                settings.Language = Array.Exists(LocalizationManager.SupportedLanguages, x => x == settings.Language)
                    ? settings.Language
                    : "tr";
                return settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AppSettings.Yukle error: " + ex);
            }
        }
        return new AppSettings();
    }

    public void Kaydet()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(DbPath))
                DbPath = AppPaths.NormalizeDatabasePath(DbPath);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            string tempFile = AppPaths.SettingsFilePath + ".tmp";
            File.WriteAllText(tempFile, json);

            if (File.Exists(AppPaths.SettingsFilePath))
                File.Replace(tempFile, AppPaths.SettingsFilePath, null);
            else
                File.Move(tempFile, AppPaths.SettingsFilePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("AppSettings.Kaydet error: " + ex);
        }
    }
}
