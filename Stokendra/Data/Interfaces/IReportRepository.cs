using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.Data.Interfaces;

/// <summary>
/// Repository interface for reporting and dashboard statistics.
/// </summary>
public interface IReportRepository
{
    /// <summary>
    /// Gets summary statistics for the dashboard asynchronously.
    /// </summary>
    /// <returns>
    /// Total card count, total stock quantity, low stock count,
    /// depleted stock count, total movement count, and today's movement count.
    /// </returns>
    Task<DashboardStats> GetDashboardStatsAsync(int fallbackThreshold = 3, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a summary report of all child stock cards asynchronously.
    /// </summary>
    Task<List<StockReportRow>> GetStockReportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a SQL dump of the database to the specified file path.
    /// </summary>
    /// <param name="destinationPath">Target .sql file path.</param>
    void ExportSqlBackup(string destinationPath);
}

/// <summary>Dashboard statistics summary.</summary>
/// <param name="TotalCards">Total number of child stock cards.</param>
/// <param name="TotalStock">Total current stock quantity.</param>
/// <param name="LowStock">Number of cards with low stock levels.</param>
/// <param name="DepletedStock">Number of cards with depleted or negative stock levels.</param>
/// <param name="TotalMovements">Total number of movements.</param>
/// <param name="TodayMovements">Number of movements made today.</param>
public record DashboardStats(
    int TotalCards,
    double TotalStock,
    int LowStock,
    int DepletedStock,
    int TotalMovements,
    int TodayMovements);

/// <summary>Stock report row data.</summary>
/// <param name="Code">Stock code.</param>
/// <param name="Name">Stock name.</param>
/// <param name="TotalEntry">Total quantity entered.</param>
/// <param name="TotalExit">Total quantity exited.</param>
/// <param name="Current">Current stock quantity.</param>
public record StockReportRow(
    string Code,
    string Name,
    double TotalEntry,
    double TotalExit,
    double Current);
