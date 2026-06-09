using FluentAssertions;
using Microsoft.Data.Sqlite;
using Stokendra.Data;
using Xunit;
using System;
using System.IO;

namespace Stokendra.Tests;

public class MigrationTests : TestBase
{
    [Fact]
    public void MigrationReplay_FromV1ToLatest_ShouldSucceed()
    {
        // Temiz bir dosya bazlı DB oluşturalım
        string dbPath = Path.Combine(Path.GetTempPath(), $"migration_test_{Guid.NewGuid()}.db");
        
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
        // Otomatik olarak V1'den V12'ye migrate etmeli.
        using (var db = new Database(dbPath))
        {
            // Hiçbir hata fırlatmadan buraya gelmeli
            // Ve yeni eklenen tablolar (örn. Notes) var olmalı
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Notes';";
            var result = cmd.ExecuteScalar();
            result.Should().NotBeNull();
        }

        if (File.Exists(dbPath)) File.Delete(dbPath);
    }

    [Fact]
    public void MigrationReplay_V13Recovery_ShouldSucceed()
    {
        // 1. Create a DB file simulating the broken V12 state:
        // - Has Turkish tables with data (StokKartlari, StokHareketleri)
        // - Has empty English tables (StockCards, StockMovements) created by the failed V12 run
        // - Version is 12
        string dbPath = Path.Combine(Path.GetTempPath(), $"migration_v13_test_{Guid.NewGuid()}.db");
        
        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            
            // Create legacy Turkish tables with data
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE StokKartlari (Id INTEGER PRIMARY KEY, KodNo TEXT, Ad TEXT);
                INSERT INTO StokKartlari (Id, KodNo, Ad) VALUES (1, 'KOD001', 'Urun 1');
                
                CREATE TABLE StokHareketleri (Id INTEGER PRIMARY KEY, StokKartId INTEGER, Tur TEXT, Miktar REAL);
                INSERT INTO StokHareketleri (Id, StokKartId, Tur, Miktar) VALUES (1, 1, 'Giris', 10.0);
                
                -- Create empty English tables to simulate the failed V12 state
                CREATE TABLE StockCards (Id INTEGER PRIMARY KEY, Code TEXT, Name TEXT);
                CREATE TABLE StockMovements (Id INTEGER PRIMARY KEY, StockCardId INTEGER, Type TEXT, Quantity REAL);
                
                PRAGMA user_version = 12;
            ";
            cmd.ExecuteNonQuery();
        }

        // 2. Initialize the Database class on this db. It should automatically update to V13,
        // drop empty English tables, rename the Turkish tables, rename columns, and normalize values.
        using (var db = new Database(dbPath))
        {
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            
            // Assert StokKartlari no longer exists
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='StokKartlari';";
            Convert.ToInt32(cmd.ExecuteScalar()).Should().Be(0);
            
            // Assert StockCards exists and has the migrated record
            cmd.CommandText = "SELECT COUNT(*) FROM StockCards;";
            Convert.ToInt32(cmd.ExecuteScalar()).Should().Be(1);
            
            // Assert columns are renamed (e.g. Ad -> Name, KodNo -> Code)
            cmd.CommandText = "SELECT Name, Code FROM StockCards WHERE Id=1;";
            using (var reader = cmd.ExecuteReader())
            {
                reader.Read().Should().BeTrue();
                reader.GetString(0).Should().Be("Urun 1");
                reader.GetString(1).Should().Be("KOD001");
            }
            
            // Assert StockMovements exists and has the migrated record
            cmd.CommandText = "SELECT COUNT(*) FROM StockMovements;";
            Convert.ToInt32(cmd.ExecuteScalar()).Should().Be(1);
            
            // Assert values are normalized (e.g. Tur 'Giris' -> Type 'Entry', StokKartId -> StockCardId)
            cmd.CommandText = "SELECT StockCardId, Type, Quantity FROM StockMovements WHERE Id=1;";
            using (var reader = cmd.ExecuteReader())
            {
                reader.Read().Should().BeTrue();
                reader.GetInt32(0).Should().Be(1);
                reader.GetString(1).Should().Be("Entry");
                reader.GetDouble(2).Should().Be(10.0);
            }
        }

        if (File.Exists(dbPath)) File.Delete(dbPath);
    }
}
