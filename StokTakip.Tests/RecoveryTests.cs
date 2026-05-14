using FluentAssertions;
using Microsoft.Data.Sqlite;
using StokTakip.Data;
using StokTakip.Data.Interfaces;
using StokTakip.Data.Repositories;
using StokTakip.Models;
using StokTakip.Infrastructure;
using System.IO;
using Xunit;

namespace StokTakip.Tests;

public class RecoveryTests : TestBase
{
    [Fact]
    public void Backup_Via_ConfigRepository_ShouldCreateValidCopy()
    {
        // 1. Mevcut test DB'sine veri ekle
        var factory = new TestDbFactory(Database.DatabasePath);
        var repo = new StockCardRepository(factory);
        repo.Add(new StokKarti { KodNo = "B-1", Ad = "Backup Test", KartTipi = "Alt" });

        // 2. Yedekleme hedefini hazırla
        string backupPath = Path.Combine(Path.GetTempPath(), $"stokendra_backup_{Guid.NewGuid()}.db");
        
        try
        {
            // 3. Pool temizle ki veriler diske yazılsın
            SqliteConnection.ClearAllPools();

            // 4. ConfigRepository üzerinden yedekle
            var configRepo = new ConfigRepository(factory);
            configRepo.CreateBackup(backupPath);
            
            // 5. Doğrula - Yedek dosyası var mı?
            File.Exists(backupPath).Should().BeTrue();

            // 6. Yedek DB'den veri okunabiliyor mu?
            var backupFactory = new TestDbFactory(backupPath);
            var backupRepo = new StockCardRepository(backupFactory);
            var cards = backupRepo.GetAll();
            cards.Should().Contain(c => c.KodNo == "B-1");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(backupPath)) try { File.Delete(backupPath); } catch { }
        }
    }

    [Fact]
    public void Corruption_Recovery_Simulation()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"corruption_test_{Guid.NewGuid()}.db");
        
        // Sağlam bir DB oluştur
        using (var db = new Database(dbPath)) { }

        // Pool'u temizle ki dosya kilidi kalksın
        SqliteConnection.ClearAllPools();

        // Dosyanın ortasına rastgele veri yazarak boz (Corruption)
        using (var stream = new FileStream(dbPath, FileMode.Open, FileAccess.Write))
        {
            stream.Seek(100, SeekOrigin.Begin);
            stream.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, 0, 4);
        }

        // Database sınıfı bozuk DB'yi açmaya çalışırken hata fırlatmalı
        // Initialize() içindeki PRAGMA quick_check bunu yakalamalı.
        Action act = () => { using var db = new Database(dbPath) { }; };
        act.Should().Throw<Exception>();

        SqliteConnection.ClearAllPools();
        if (File.Exists(dbPath)) try { File.Delete(dbPath); } catch { }
    }

    [Fact]
    public void WAL_Recovery_Simulation()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"wal_recovery_{Guid.NewGuid()}.db");

        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE Test (Id INT);";
            cmd.ExecuteNonQuery();
            
            cmd.CommandText = "INSERT INTO Test (Id) VALUES (1);";
            cmd.ExecuteNonQuery();
        } 

        // Yeni Database nesnesi ile açtığımızda veri kurtarılmış olmalı
        using (var db = new Database(dbPath))
        {
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Test;";
            var count = Convert.ToInt32(cmd.ExecuteScalar());
            count.Should().Be(1);
        }

        SqliteConnection.ClearAllPools();
        if (File.Exists(dbPath)) try { File.Delete(dbPath); } catch { }
    }
}
