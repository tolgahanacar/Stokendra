using StokTakip.Models;

namespace StokTakip.Data.Interfaces;

/// <summary>
/// Servis kaydı CRUD ve sorgulama işlemleri için repository arayüzü.
/// </summary>
public interface IServiceRecordRepository
{
    /// <summary>
    /// Servis kayıtlarını getirir. Tüm parametreler opsiyoneldir.
    /// </summary>
    /// <param name="startDate">Başlangıç tarihi filtresi (dahil).</param>
    /// <param name="endDate">Bitiş tarihi filtresi (hariç).</param>
    /// <param name="searchTerm">Cihaz adı, seri no, firma, sorun veya sonuç içinde arama terimi.</param>
    List<ServisKaydi> GetAll(DateTime? startDate = null, DateTime? endDate = null, string? searchTerm = null);

    /// <summary>Yeni bir servis kaydı ekler.</summary>
    void Add(ServisKaydi kayit);

    /// <summary>Birden fazla servis kaydını toplu ekler.</summary>
    void AddBulk(IEnumerable<ServisKaydi> kayitlar);

    /// <summary>Mevcut bir servis kaydını günceller.</summary>
    void Update(ServisKaydi kayit);

    /// <summary>Belirtilen ID'ye sahip servis kaydını siler.</summary>
    void Delete(int id);
}
