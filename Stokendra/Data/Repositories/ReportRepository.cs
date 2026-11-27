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
                    COALESCE(SUM(CASE
                        WHEN m.Type IN ('Entry', 'Giris', 'Giriş') THEN m.Quantity
                        WHEN m.Type IN ('Exit', 'Cikis', 'Çıkış') THEN -m.Quantity
                        ELSE 0
                    END), 0) AS Balance
                FROM StockCards s
                LEFT JOIN StockMovements m ON s.Id = m.StockCardId
                WHERE s.CardType = 'Child'
                GROUP BY s.Id
            )
            SELECT
                COUNT(*) AS TotalCards,
                COALESCE(SUM(Balance), 0) AS TotalStock,
                COALESCE(SUM(CASE WHEN Balance <= CASE WHEN MinStock > 0 THEN MinStock ELSE @Fallback END THEN 1 ELSE 0 END), 0) AS LowStock,
                COALESCE(SUM(CASE WHEN Balance <= 0 THEN 1 ELSE 0 END), 0) AS DepletedStock,
                (SELECT COUNT(*) FROM StockMovements) AS TotalMovements,
                (SELECT COUNT(*) FROM StockMovements WHERE Date >= date('now', 'localtime') AND Date < date('now', 'localtime', '+1 day')) AS TodayMovements
            FROM StockBalances
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
            COALESCE(SUM(CASE WHEN h.Type IN ('Entry', 'Giris', 'Giriş') THEN h.Quantity ELSE 0 END), 0) as TotalEntry,
            COALESCE(SUM(CASE WHEN h.Type IN ('Exit', 'Cikis', 'Çıkış') THEN h.Quantity ELSE 0 END), 0) as TotalExit,
            COALESCE(SUM(CASE
                WHEN h.Type IN ('Entry', 'Giris', 'Giriş') THEN h.Quantity
                WHEN h.Type IN ('Exit', 'Cikis', 'Çıkış') THEN -h.Quantity
                ELSE 0
            END), 0) as Current
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
        if (File.Exists(destinationPath)) File.Delete(destinationPath);
        if (File.Exists(destinationPath + "-wal")) File.Delete(destinationPath + "-wal");
        if (File.Exists(destinationPath + "-shm")) File.Delete(destinationPath + "-shm");

        using var source = connectionFactory.CreateConnection();
        var csb = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Pooling = false
        };
        using var destination = new Microsoft.Data.Sqlite.SqliteConnection(csb.ToString());
        destination.Open();
        source.BackupDatabase(destination);
    }
}
