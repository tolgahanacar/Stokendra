using System.Text.Json;

namespace StokTakip;

public class AppSettings
{
    public string DbPath { get; set; } = "";
    public string Language { get; set; } = "tr";
    public string CompanyName { get; set; } = "";
    public string Theme { get; set; } = "dark";

    private static readonly string SettingsFile =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ayarlar.json");

    public static AppSettings Yukle()
    {
        if (File.Exists(SettingsFile))
        {
            try
            {
                var json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch { }
        }
        return new AppSettings();
    }

    public void Kaydet()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFile, json);
    }
}
