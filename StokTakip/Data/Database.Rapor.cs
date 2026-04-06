using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;
using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Data;

public sealed partial class Database
{

    public (int toplamKart, double toplamStok, int dusuk, int tukenmis, int toplamHareket, int bugunHareket) DashboardIstatistikleriGetir()
    {
        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, @"
                WITH hareket_ozet AS (
                    SELECT
                        StokKartId,
                        SUM(CASE WHEN Tur='Giris' THEN Miktar WHEN Tur='Cikis' THEN -Miktar ELSE 0 END) AS Mevcut
                    FROM StokHareketleri
                    GROUP BY StokKartId
                ),
                alt_stok AS (
                    SELECT
                        s.Id,
                        s.MinStok,
                        COALESCE(h.Mevcut, 0) AS Mevcut
                    FROM StokKartlari s
                    LEFT JOIN hareket_ozet h ON h.StokKartId = s.Id
                    WHERE COALESCE(s.KartTipi, 'Alt')='Alt'
                )
                SELECT
                    (SELECT COUNT(*) FROM alt_stok),
                    (SELECT COALESCE(SUM(CASE WHEN Mevcut > 0 THEN Mevcut ELSE 0 END), 0) FROM alt_stok),
                    (SELECT COUNT(*) FROM alt_stok WHERE Mevcut > 0 AND Mevcut <= CASE WHEN MinStok > 0 THEN MinStok ELSE 3 END),
                    (SELECT COUNT(*) FROM alt_stok WHERE Mevcut <= 0),
                    (SELECT COUNT(*) FROM StokHareketleri),
                    (SELECT COUNT(*) FROM StokHareketleri WHERE Tarih LIKE $today);");
            command.Parameters.AddWithValue("$today", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "%");

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return (
                    reader.GetInt32(0),
                    reader.GetDouble(1),
                    reader.GetInt32(2),
                    reader.GetInt32(3),
                    reader.GetInt32(4),
                    reader.GetInt32(5));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DashboardIstatistikleriGetir error: " + ex);
        }

        return (0, 0, 0, 0, 0, 0);
    }


    public void ExportSqlBackup(string destinationPath)
    {
        string normalizedPath = AppPaths.NormalizeWritableFilePath(destinationPath);
        var builder = new StringBuilder();

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();

        builder.AppendLine("-- Stokendra SQL Backup");
        builder.AppendLine($"-- Date: {DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture)}");
        builder.AppendLine();

        foreach (string table in SqlBackupTables)
        {
            using var schemaCommand = CreateCommand(connection, transaction,
                "SELECT sql FROM sqlite_master WHERE type='table' AND name=$name");
            schemaCommand.Parameters.AddWithValue("$name", table);
            string? createSql = schemaCommand.ExecuteScalar()?.ToString();
            if (string.IsNullOrWhiteSpace(createSql))
                continue;

            builder.AppendLine($"DROP TABLE IF EXISTS {table};");
            builder.AppendLine(createSql + ";");

            using var dataCommand = CreateCommand(connection, transaction, $"SELECT * FROM {table}");
            using var reader = dataCommand.ExecuteReader();
            while (reader.Read())
            {
                var values = new List<string>(reader.FieldCount);
                for (int i = 0; i < reader.FieldCount; i++)
                    values.Add(ToSqlLiteral(reader.GetValue(i)));

                builder.AppendLine($"INSERT INTO {table} VALUES ({string.Join(",", values)});");
            }

            builder.AppendLine();
        }

        File.WriteAllText(normalizedPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
