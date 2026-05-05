using StokTakip.Models;

namespace StokTakip.Data.Interfaces;

/// <summary>
/// Uygulama yapılandırma ve yardımcı veri işlemleri için repository arayüzü.
/// </summary>
public interface IConfigRepository
{
    /// <summary>Belirtilen anahtara ait yapılandırma değerini getirir. Bulunamazsa <paramref name="defaultValue"/> döner.</summary>
    string GetConfig(string key, string defaultValue = "");

    /// <summary>Belirtilen anahtara ait yapılandırma değerini kaydeder veya günceller.</summary>
    void SetConfig(string key, string value);

    /// <summary>Tüm birimleri alfabetik sırada getirir.</summary>
    List<Birim> GetUnits();

    /// <summary>Veritabanının SQLite yedeğini belirtilen dosya yoluna kopyalar.</summary>
    void CreateBackup(string destinationPath);

    /// <summary>Denetim günlüğüne yeni bir kayıt ekler.</summary>
    void WriteAuditLog(string type, string table, int recordId, string detail);
}
