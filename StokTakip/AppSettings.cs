using System.Text.Json;

namespace StokTakip;

public class AppSettings
{
    public string DbPath { get; set; } = "";
    public string Language { get; set; } = "tr";
    public string CompanyName { get; set; } = "";
    public string Theme { get; set; } = "dark";

    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "Stokendra", 
        "ayarlar.json");

    static AppSettings()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Stokendra");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    public static AppSettings Yukle()
    {
        if (File.Exists(SettingsFile))
        {
            try
            {
                var json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
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
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("AppSettings.Kaydet error: " + ex);
        }
    }
}
