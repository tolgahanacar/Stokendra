using Microsoft.Data.Sqlite;
using StokTakip.Data.Interfaces;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace StokTakip.Data;

/// <summary>
/// Veritabanı şema yönetimi ve versiyon geçişlerini (migration) yönetir.
/// </summary>
public sealed class DatabaseMigrator
{
    private readonly IDbConnectionFactory _connectionFactory;
    private const int TargetVersion = 10;

    public DatabaseMigrator(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Migrate()
    {
        using var connection = _connectionFactory.CreateConnection();
        
        // Versiyon kontrolü (Transaction dışında PRAGMA okuması daha güvenli)
        int currentVersion = GetVersion(connection);
        if (currentVersion >= TargetVersion) return;

        using var transaction = connection.BeginTransaction();
        try
        {
            if (currentVersion == 0)
            {
                CreateInitialSchema(connection, transaction);
                currentVersion = 1;
            }

            // Artımlı migration (Örnek v1->v10)
            // Bu kısım projenin mevcut Database.Migrations.cs mantığını atomik hale getirir
            RunMigrations(connection, transaction, currentVersion);

            SetVersion(connection, transaction, TargetVersion);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            throw new InvalidOperationException("Veritabanı yükseltme işlemi başarısız oldu. İşlemler geri alındı.", ex);
        }
    }

    private void RunMigrations(SqliteConnection conn, SqliteTransaction trans, int startVersion)
    {
        // Örnek: V1'den V10'a kadar tüm adımları burada çalıştır
        // Bu metod içinde her adım için hata fırlatılması transaction rollback tetikler.
        // Mevcut Database.Migrations.cs içindeki logic buraya taşınmalıdır.
    }

    private int GetVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    private void SetVersion(SqliteConnection conn, SqliteTransaction trans, int version)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }

    private void CreateInitialSchema(SqliteConnection conn, SqliteTransaction trans)
    {
        // Database.Migrations.cs içindeki EnsureSchema buraya taşınır
    }
}
