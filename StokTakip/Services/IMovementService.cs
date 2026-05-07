using StokTakip.Models;

namespace StokTakip.Services;

/// <summary>
/// Stok hareketleri için iş mantığı arayüzü.
/// </summary>
public interface IMovementService
{
    /// <summary>
    /// Filtrelenmiş ve sayfalanmış stok hareketleri listesini döndürür.
    /// </summary>
    PagedResult<StokHareketi> GetPagedMovements(
        DateTime? startDate,
        DateTime? endDate,
        int? stockCardId,
        string? department,
        string? movementType,
        string? searchTerm,
        int page,
        int pageSize);

    /// <summary>
    /// Excel dosyasından stok hareketlerini içe aktarır.
    /// </summary>
    Task<(int Imported, int Skipped, List<string> Warnings)> ImportMovementsFromXlsxAsync(string filePath);
}
