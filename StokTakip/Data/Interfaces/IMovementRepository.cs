using StokTakip.Models;

namespace StokTakip.Data.Interfaces;

/// <summary>
/// Stok hareketi CRUD ve sorgulama işlemleri için repository arayüzü.
/// </summary>
public interface IMovementRepository
{
    /// <summary>
    /// Belirtilen filtrelere göre stok hareketlerini getirir.
    /// Tüm parametreler opsiyoneldir; verilmezse filtre uygulanmaz.
    /// </summary>
    /// <param name="stockCardId">Stok kartı ID filtresi.</param>
    /// <param name="startDate">Başlangıç tarihi filtresi (dahil).</param>
    /// <param name="endDate">Bitiş tarihi filtresi (hariç).</param>
    /// <param name="department">Departman adı filtresi.</param>
    /// <param name="movementType">Hareket türü filtresi: Giris / Cikis / Bos.</param>
    List<StokHareketi> GetAll(
        int? stockCardId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? department = null,
        string? movementType = null,
        string? category = null);

    /// <summary>Stok hareketlerini asenkron olarak getirir.</summary>
    Task<List<StokHareketi>> GetAllAsync(
        int? stockCardId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? department = null,
        string? movementType = null,
        string? category = null);

    /// <summary>Tek bir stok hareketi ekler.</summary>
    void Add(StokHareketi hareket);

    /// <summary>Tek bir stok hareketini asenkron olarak ekler.</summary>
    Task AddAsync(StokHareketi hareket);

    /// <summary>Birden fazla stok hareketini tek transaction içinde toplu ekler.</summary>
    void AddBulk(IEnumerable<StokHareketi> hareketler);

    /// <summary>Birden fazla stok hareketini asenkron olarak toplu ekler.</summary>
    Task AddBulkAsync(IEnumerable<StokHareketi> hareketler);

    /// <summary>Mevcut bir stok hareketini günceller.</summary>
    void Update(StokHareketi hareket);

    /// <summary>Tek bir stok hareketini siler.</summary>
    void Delete(int id);

    /// <summary>Birden fazla stok hareketini toplu siler.</summary>
    void DeleteBulk(IEnumerable<int> ids);

    /// <summary>
    /// Birden fazla stok hareketini tek bir atomik transaction içinde toplu günceller.
    /// Kısmi başarı yoktur: ya hepsi güncellenir ya da hiçbiri.
    /// </summary>
    void UpdateBulk(IEnumerable<StokHareketi> hareketler);

    /// <summary>Son 7 güne ait günlük giriş/çıkış özetlerini asenkron olarak getirir.</summary>
    Task<List<(DateTime Date, double Entry, double Exit)>> GetLast7DaysSummaryAsync();

    /// <summary>Teslim edilen kişilerin benzersiz listesini getirir.</summary>
    List<string> GetDeliveredPersons();
}
