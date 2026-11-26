using System.Text.Json;

namespace Stokendra;

/// <summary>
/// JSON tabanlı uygulama ayarları. <c>AppData/Local/Stokendra/ayarlar.json</c> dosyasında saklanır.
/// </summary>
public class AppSettings
{
    /// <summary>SQLite veritabanı dosya yolu.</summary>
    public string DbPath { get; set; } = "";

    /// <summary>Arayüz dili kodu (örn. "tr", "en").</summary>
    public string Language { get; set; } = "tr";

    /// <summary>Şirket adı (raporlarda kullanılır).</summary>
    public string CompanyName { get; set; } = "";

    /// <summary>Tema adı (şu an yalnızca "dark" desteklenmektedir).</summary>
    public string Theme { get; set; } = "dark";

    /// <summary>Otomatik yedekleme hedef klasörü. Boşsa otomatik yedekleme devre dışıdır.</summary>
    public string AutoBackupPath { get; set; } = "";

    /// <summary>Düşük stok uyarı eşiği.</summary>
    public int LowStockThreshold { get; set; } = 3;
    
    /// <summary>Yönetici güvenlik kodu.</summary>
    public string MasterSecurityCode { get; set; } = "1951"; // Varsayılan kod

    /// <summary>Son başarılı yedekleme tarihi.</summary>
    public DateTime? LastBackupDate { get; set; }

    /// <summary>Geri bildirim ve hata raporlarının iletileceği e-posta adresi.</summary>
    public string FeedbackEmail { get; set; } = "2005.tolgahan@gmail.com";

    /// <summary>Özel bildirim webhook adresi (opsiyonel).</summary>
    public string FeedbackWebhookUrl { get; set; } = "";

    /// <summary>Web3Forms form erişim anahtarı (opsiyonel).</summary>
    public string Web3FormsAccessKey { get; set; } = "";

    static AppSettings()
    {
        _ = AppPaths.ApplicationDataDirectory;
    }

    /// <summary>
    /// Ayarları disk'ten yükler. Dosya yoksa veya bozuksa varsayılan ayarları döner.
    /// </summary>
    public static AppSettings Load()
    {
        if (File.Exists(AppPaths.SettingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(AppPaths.SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                try
                {
                    settings.DbPath = string.IsNullOrWhiteSpace(settings.DbPath)
                        ? ""
                        : AppPaths.NormalizeDatabasePath(settings.DbPath);
                }
                catch (Exception pathEx)
                {
                    AppLogger.LogError("AppSettings.Load DbPath normalize warning: " + pathEx);
                    settings.DbPath = "";
                }
                settings.MasterSecurityCode ??= "1951";
                settings.CompanyName ??= "";
                settings.AutoBackupPath ??= "";
                settings.Language = Array.Exists(LocalizationManager.SupportedLanguages, x => x == settings.Language)
                    ? settings.Language
                    : "tr";
                return settings;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("AppSettings.Load error: " + ex);
            }
        }
        return new AppSettings();
    }

    /// <summary>
    /// Ayarları disk'e kaydeder. Atomik yazma için geçici dosya kullanır.
    /// </summary>
    /// <returns>Kayıt başarılıysa true, hata oluşursa false döner.</returns>
    public bool Save()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(DbPath))
                DbPath = AppPaths.NormalizeDatabasePath(DbPath);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            string tempFile = $"{AppPaths.SettingsFilePath}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(tempFile, json);

            if (File.Exists(AppPaths.SettingsFilePath))
            {
                try
                {
                    File.Replace(tempFile, AppPaths.SettingsFilePath, null);
                }
                catch
                {
                    // Fallback for non-NTFS drives (FAT32, exFAT, network shares)
                    File.Copy(tempFile, AppPaths.SettingsFilePath, true);
                    try { File.Delete(tempFile); } catch { }
                }
            }
            else
            {
                File.Move(tempFile, AppPaths.SettingsFilePath);
            }

            return true;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("AppSettings.Save error: " + ex);
            return false;
        }
    }

    /// <summary>
    /// Ayarları asenkron olarak disk'e kaydeder.
    /// </summary>
    /// <returns>Kayıt başarılıysa true, hata oluşursa false döner.</returns>
    public async Task<bool> SaveAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(DbPath))
                DbPath = AppPaths.NormalizeDatabasePath(DbPath);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            string tempFile = $"{AppPaths.SettingsFilePath}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllTextAsync(tempFile, json);

            if (File.Exists(AppPaths.SettingsFilePath))
            {
                try
                {
                    File.Replace(tempFile, AppPaths.SettingsFilePath, null);
                }
                catch
                {
                    // Fallback for non-NTFS drives (FAT32, exFAT, network shares)
                    File.Copy(tempFile, AppPaths.SettingsFilePath, true);
                    try { File.Delete(tempFile); } catch { }
                }
            }
            else
            {
                File.Move(tempFile, AppPaths.SettingsFilePath);
            }

            return true;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("AppSettings.SaveAsync error: " + ex);
            return false;
        }
    }
}
