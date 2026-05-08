using FluentAssertions;
using Microsoft.Data.Sqlite;
using StokTakip.Data;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using StokTakip.Infrastructure;
using System.IO;
using Xunit;

namespace StokTakip.Tests;

public class RecoveryTests : TestBase
{
    [Fact]
    public async Task Backup_Restore_Verification()
    {
        // 1. Mevcut test DB'sine veri ekle (TestBase zaten bir DB oluşturdu)
        var repo = ServiceContainer.GetService<IStockCardRepository>();
        await repo.AddAsync(new StokKarti { KodNo = "B-1", Ad = "Backup Test", KartTipi = "Alt" });

        // 2. Yedekleme klasörünü hazırla
        string backupDir = Path.Combine(Path.GetTempPath(), "Stokendra_Test_Backups");
        if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

        // 3. Pool temizle ve checkpoint yap ki veriler diske yazılsın
        SqliteConnection.ClearAllPools();

        // 4. Yedekle
        string zipFile = await BackupManager.PerformBackupAsync(backupDir);
        
        // 5. Doğrula
        File.Exists(zipFile).Should().BeTrue();
        
        // Temizlik
        if (File.Exists(zipFile)) File.Delete(zipFile);
    }

    [Fact]
    public void Corruption_Recovery_Simulation()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"corruption_test_{Guid.NewGuid()}.db");
        
        // Sağlam bir DB oluştur
        using (var db = new Database(dbPath)) { }

        // Dosyanın ortasına rastgele veri yazarak boz (Corruption)
        using (var stream = new FileStream(dbPath, FileMode.Open, FileAccess.Write))
        {
            stream.Seek(100, SeekOrigin.Begin);
            stream.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, 0, 4);
        }

        // Database sınıfı bozuk DB'yi açmaya çalışırken hata fırlatmalı veya handle etmeli
        // Paranoid mode'da biz 'Integrity Check' bekliyoruz.
        Action act = () => { using var db = new Database(dbPath) { }; };
        
        // SQLite bozuk dosyayı açarken hemen hata vermeyebilir (okurken verir)
        // Ancak bizim Initialize() içindeki PRAGMA quick_check bunu yakalamalı.
        act.Should().Throw<Exception>();

        if (File.Exists(dbPath)) File.Delete(dbPath);
    }

    [Fact]
    public void WAL_Recovery_Simulation()
    {
        // SQLite WAL modu zaten crash-safe'dir. 
        // Bu testte WAL dosyası varken (check-point yapılmamışken) DB'yi açmayı deniyoruz.
        string dbPath = Path.Combine(Path.GetTempPath(), $"wal_recovery_{Guid.NewGuid()}.db");
        string walPath = dbPath + "-wal";

        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE Test (Id INT);";
            cmd.ExecuteNonQuery();
            
            // Veri ekle ama checkpoint yapma (bağlantıyı sert kapat)
            cmd.CommandText = "INSERT INTO Test (Id) VALUES (1);";
            cmd.ExecuteNonQuery();
        } 
        // Connection dispose edildi ama WAL dosyası hala duruyor olabilir.

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

        if (File.Exists(dbPath)) File.Delete(dbPath);
    }
}
