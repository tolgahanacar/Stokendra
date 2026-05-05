using StokTakip.Models;

namespace StokTakip.Data.Interfaces;

/// <summary>
/// Stok kartı CRUD işlemleri için repository arayüzü.
/// </summary>
public interface IStockCardRepository
{
    /// <summary>Tüm stok kartlarını getirir.</summary>
    List<StokKarti> GetAll();

    /// <summary>Tüm stok kartlarını asenkron olarak getirir.</summary>
    Task<List<StokKarti>> GetAllAsync();

    /// <summary>Sadece üst kartları getirir (KartTipi = Ust).</summary>
    List<StokKarti> GetParentCards();

    /// <summary>Sadece üst kartları asenkron olarak getirir.</summary>
    Task<List<StokKarti>> GetParentCardsAsync();

    /// <summary>Alt kartları getirir. <paramref name="parentId"/> verilirse sadece o üst karta ait alt kartlar döner.</summary>
    List<StokKarti> GetChildCards(int? parentId = null);

    /// <summary>Alt kartları asenkron olarak getirir.</summary>
    Task<List<StokKarti>> GetChildCardsAsync(int? parentId = null);

    /// <summary>Belirtilen ID'ye sahip stok kartını getirir. Bulunamazsa <c>null</c> döner.</summary>
    StokKarti? GetById(int id);

    /// <summary>Yeni bir stok kartı ekler. Eklenen kaydın ID'si <paramref name="stokKarti"/>.Id alanına yazılır.</summary>
    void Add(StokKarti stokKarti);

    /// <summary>Yeni bir stok kartını asenkron olarak ekler.</summary>
    Task AddAsync(StokKarti stokKarti);

    /// <summary>Mevcut bir stok kartını günceller.</summary>
    void Update(StokKarti stokKarti);

    /// <summary>Belirtilen ID'ye sahip stok kartını siler. Bağlı hareketler de silinir.</summary>
    void Delete(int id);

    /// <summary>Bir sonraki otomatik stok kodunu üretir (örn. "042").</summary>
    string GetNextCode();
}
