using StokTakip.Models;

namespace StokTakip.Services;

/// <summary>
/// Stok kartları için iş mantığı arayüzü.
/// </summary>
public interface IStockCardService
{
    /// <summary>
    /// Filtrelenmiş ve sayfalanmış stok kartı listesini döndürür.
    /// </summary>
    PagedResult<StokKarti> GetPagedStocks(string? searchTerm, int page, int pageSize);

    /// <summary>
    /// Tüm stok kartlarını asenkron olarak getirir.
    /// </summary>
    Task<List<StokKarti>> GetAllAsync();

    /// <summary>
    /// Düşük stoklu kart sayısını döndürür.
    /// </summary>
    int GetLowStockCount();
}

/// <summary>Sayfalanmış sonuç modeli.</summary>
public record PagedResult<T>(List<T> Items, int TotalCount, int TotalPages, int CurrentPage);
