using Dapper;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.Data.Repositories;

public sealed class ReportRepository(IDbConnectionFactory connectionFactory) : IReportRepository
{
    private class DashboardStatsRecord
    {
        public int TotalCards { get; set; }
        public double TotalStock { get; set; }
        public int LowStock { get; set; }
        public int DepletedStock { get; set; }
        public int TotalMovements { get; set; }
        public int TodayMovements { get; set; }
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(int fallbackThreshold = 3, CancellationToken cancellationToken = default)
    {
        using var conn = connectionFactory.CreateConnection();
        
        var sql = """
            WITH StockBalances AS (
                SELECT 
                    s.Id,
                    s.MinStock,
                    COALESCE(SUM(CASE WHEN m.Type = 'Entry' THEN m.Quantity ELSE -m.Quantity END), 0) AS Balance
                FROM StockCards s
                LEFT JOIN StockMovements m ON s.Id = m.StockCardId
                WHERE s.CardType = 'Child'
                GROUP BY s.Id
            )
            SELECT 
                (SELECT COUNT(*) FROM StockCards WHERE CardType = 'Child') AS TotalCards,
                (SELECT COALESCE(SUM(CASE WHEN Type = 'Entry' THEN Quantity ELSE -Quantity END), 0) FROM StockMovements) AS TotalStock,
                (SELECT COUNT(*) FROM StockBalances WHERE Balance <= CASE WHEN MinStock > 0 THEN MinStock ELSE @Fallback END) AS LowStock,
                (SELECT COUNT(*) FROM StockBalances WHERE Balance <= 0) AS DepletedStock,
                (SELECT COUNT(*) FROM StockMovements) AS TotalMovements,
                (SELECT COUNT(*) FROM StockMovements WHERE DATE(Date) = DATE('now')) AS TodayMovements
            """;
            
        var record = await conn.QuerySingleOrDefaultAsync<DashboardStatsRecord>(new CommandDefinition(
            sql, 
            new { Fallback = fallbackThreshold }, 
            cancellationToken: cancellationToken));
            
        if (record == null)
            return new DashboardStats(0, 0, 0, 0, 0, 0);

        return new DashboardStats(
            TotalCards: record.TotalCards,
            TotalStock: record.TotalStock,
            LowStock: record.LowStock,
            DepletedStock: record.DepletedStock,
            TotalMovements: record.TotalMovements,
            TodayMovements: record.TodayMovements
        );
    }

    public async Task<List<StockReportRow>> GetStockReportAsync(CancellationToken cancellationToken = default)
    {
        using var conn = connectionFactory.CreateConnection();
        var sql = """
            SELECT s.Code, s.Name,
            COALESCE(SUM(CASE WHEN h.Type = 'Entry' THEN h.Quantity ELSE 0 END), 0) as TotalEntry,
            COALESCE(SUM(CASE WHEN h.Type = 'Exit' THEN h.Quantity ELSE 0 END), 0) as TotalExit,
            COALESCE(SUM(CASE WHEN h.Type = 'Entry' THEN h.Quantity ELSE -h.Quantity END), 0) as Current
            FROM StockCards s
            LEFT JOIN StockMovements h ON s.Id = h.StockCardId
            WHERE s.CardType = 'Child'
            GROUP BY s.Id
            ORDER BY s.Code
            """;
        
        var result = await conn.QueryAsync<StockReportRow>(new CommandDefinition(
            sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public void ExportSqlBackup(string destinationPath)
    {
        // Handled via SQLite CreateBackup native functionality
    }
}
