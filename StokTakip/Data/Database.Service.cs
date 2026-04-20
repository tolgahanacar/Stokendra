using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Data;

public sealed partial class Database
{

    public List<ServisKaydi> ServisKayitlariniGetir(DateTime? baslangic = null, DateTime? bitis = null, string? arama = null)
    {
        var liste = new List<ServisKaydi>();

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, BuildServiceQuery(baslangic, bitis, arama));

            if (baslangic.HasValue)
                command.Parameters.AddWithValue("$bas", baslangic.Value.ToString(DateFormat, CultureInfo.InvariantCulture));
            if (bitis.HasValue)
                command.Parameters.AddWithValue("$bit", bitis.Value.ToString(DateFormat, CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(arama))
                command.Parameters.AddWithValue("$ara", $"%{arama.Trim()}%");

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                liste.Add(new ServisKaydi
                {
                    Id = reader.GetInt32(0),
                    CihazAdi = reader.GetString(1),
                    SeriNumarasi = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Firma = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    BakimTarihi = ParseDateSafe(reader.GetString(4)),
                    Sorun = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Sonuc = reader.IsDBNull(6) ? "" : reader.GetString(6)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("ServisKayitlariniGetir error: " + ex);
        }

        return liste;
    }


    public void ServisKaydiEkle(ServisKaydi kayit)
    {
        TopluServisKaydiEkle(new[] { kayit });
    }


    public void ServisKaydiGuncelle(ServisKaydi kayit)
    {
        ServisKaydi normalized = NormalizeServiceRecord(kayit);

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            ValidateServiceRecord(normalized);

            using var command = CreateCommand(connection, transaction, @"
                UPDATE ServisKayitlari
                SET
                    CihazAdi=$ca,
                    SeriNumarasi=$sn,
                    Firma=$f,
                    BakimTarihi=$bt,
                    Sorun=$sr,
                    Sonuc=$sc
                WHERE Id=$id");
            BindServisKaydiParameters(command, normalized, includeId: true);

            if (command.ExecuteNonQuery() == 0)
                throw new InvalidOperationException(L("record_not_found"));

            InsertAuditLog(connection, transaction, "GUNCELLE", "ServisKayitlari", normalized.Id, normalized.CihazAdi);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("ServisKaydiGuncelle error: " + ex);
            throw;
        }
    }


    public void ServisKaydiSil(int id)
    {
        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            ServisKaydi? mevcut = GetServisKaydiById(connection, transaction, id);
            if (mevcut == null)
                throw new InvalidOperationException(L("record_not_found"));

            using var command = CreateCommand(connection, transaction, "DELETE FROM ServisKayitlari WHERE Id=$id");
            command.Parameters.AddWithValue("$id", id);
            if (command.ExecuteNonQuery() == 0)
                throw new InvalidOperationException(L("record_not_found"));

            InsertAuditLog(connection, transaction, "SIL", "ServisKayitlari", id, mevcut.CihazAdi);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("ServisKaydiSil error: " + ex);
            throw;
        }
    }
}
