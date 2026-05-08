using FluentAssertions;
using Microsoft.Data.Sqlite;
using StokTakip.Data;
using Xunit;

namespace StokTakip.Tests;

public class MigrationTests : TestBase
{
    [Fact]
    public void MigrationReplay_FromV1ToLatest_ShouldSucceed()
    {
        // Temiz bir dosya bazlı DB oluşturalım
        string dbPath = Path.Combine(Path.GetTempPath(), $"migration_test_{Guid.NewGuid()}.db");
        
        // İlk başta sadece V1 şemasını kuran bir yapı simüle edelim
        // Not: Mevcut Database sınıfı otomatik Initialize() çağırıyor. 
        // Migration testi için Database sınıfının içindeki private metodları çağırmamız gerekecek.
        // Bu yüzden Database sınıfını 'partial' yaptık (zaten öyleydi).
        
        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            
            // Manuel V1 kurulumu (Sadece temel tablolar)
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE TABLE StokKartlari (Id INTEGER PRIMARY KEY, KodNo TEXT, Ad TEXT);";
            cmd.ExecuteNonQuery();
            
            cmd.CommandText = "PRAGMA user_version = 1;";
            cmd.ExecuteNonQuery();
        }

        // Şimdi Database sınıfını bu dosya üzerinde başlatalım. 
        // Otomatik olarak V1'den V10'a migrate etmeli.
        using (var db = new Database(dbPath))
        {
            // Hiçbir hata fırlatmadan buraya gelmeli
            // Ve yeni eklenen tablolar (örn. Notlar V3'te geldi) var olmalı
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Notlar';";
            var result = cmd.ExecuteScalar();
            result.Should().NotBeNull();
        }

        if (File.Exists(dbPath)) File.Delete(dbPath);
    }
}
