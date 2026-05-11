namespace StokTakip.Data.Interfaces;

/// <summary>
/// Raporlama ve dashboard istatistikleri için repository arayüzü.
/// </summary>
public interface IReportRepository
{
    /// <summary>
    /// Dashboard için özet istatistikleri asenkron olarak getirir.
    /// </summary>
    /// <returns>
    /// Toplam kart sayısı, toplam stok miktarı, düşük stok sayısı,
    /// tükenmiş stok sayısı, toplam hareket sayısı ve bugünkü hareket sayısı.
    /// </returns>
    Task<DashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tüm alt stok kartlarının özet raporunu asenkron olarak getirir.
    /// </summary>
    Task<List<StockReportRow>> GetStockReportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Veritabanının SQL dump'ını belirtilen dosya yoluna yazar.
    /// </summary>
    /// <param name="destinationPath">Hedef .sql dosya yolu.</param>
    void ExportSqlBackup(string destinationPath);
}

/// <summary>Dashboard istatistik özeti.</summary>
/// <param name="TotalCards">Toplam alt stok kartı sayısı.</param>
/// <param name="TotalStock">Toplam mevcut stok miktarı.</param>
/// <param name="LowStock">Düşük stoklu kart sayısı.</param>
/// <param name="DepletedStock">Tükenmiş stoklu kart sayısı.</param>
/// <param name="TotalMovements">Toplam hareket sayısı.</param>
/// <param name="TodayMovements">Bugünkü hareket sayısı.</param>
public record DashboardStats(
    int TotalCards,
    double TotalStock,
    int LowStock,
    int DepletedStock,
    int TotalMovements,
    int TodayMovements);

/// <summary>Stok raporu satırı.</summary>
/// <param name="Code">Stok kodu.</param>
/// <param name="Name">Stok adı.</param>
/// <param name="TotalEntry">Toplam giriş miktarı.</param>
/// <param name="TotalExit">Toplam çıkış miktarı.</param>
/// <param name="Current">Mevcut stok miktarı.</param>
public record StockReportRow(
    string Code,
    string Name,
    double TotalEntry,
    double TotalExit,
    double Current);
