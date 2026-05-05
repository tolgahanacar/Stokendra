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

    public List<StokHareketi> HareketleriGetir(
        int? stokKartId = null,
        DateTime? baslangic = null,
        DateTime? bitis = null,
        string? departman = null,
        string? tur = null)
    {
        var liste = new List<StokHareketi>();

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, BuildMovementQuery(stokKartId, baslangic, bitis, departman, tur));

            if (stokKartId.HasValue)
                command.Parameters.AddWithValue("$kid", stokKartId.Value);
            if (baslangic.HasValue)
                command.Parameters.AddWithValue("$ts", baslangic.Value.ToString(DateFormat, CultureInfo.InvariantCulture));
            if (bitis.HasValue)
                command.Parameters.AddWithValue("$te", bitis.Value.ToString(DateFormat, CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(departman))
                command.Parameters.AddWithValue("$dep", departman.Trim());
            if (!string.IsNullOrWhiteSpace(tur))
                command.Parameters.AddWithValue("$tur", tur.Trim());

            using var reader = command.ExecuteReader();
            while (reader.Read())
                liste.Add(ReadMovement(reader));
        }
        catch (Exception ex)
        {
            AppLogger.LogError("HareketleriGetir error: " + ex);
        }

        return liste;
    }


    public void HareketEkle(StokHareketi hareket)
    {
        TopluHareketEkle(new[] { hareket });
    }


    public void TopluHareketEkle(IEnumerable<StokHareketi> hareketler)
    {
        var liste = hareketler?.Select(NormalizeMovement).ToList() ?? new List<StokHareketi>();
        if (liste.Count == 0)
            throw new InvalidOperationException(L("bulk_operation_empty"));

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var hareket in liste)
            {
                ValidateMovement(connection, transaction, hareket);
                EnsureMovementWillNotCreateNegativeStock(connection, transaction, hareket.StokKartId, MovementImpact(hareket));

                using var command = CreateCommand(connection, transaction, @"
                    INSERT INTO StokHareketleri
                        (StokKartId, Tur, Miktar, KimeVerildi, Departman, Tarih, Aciklama)
                    VALUES
                        ($sk, $t, $m, $kv, $d, $ta, $ac)");
                BindMovementParameters(command, hareket, includeId: false);
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            AppLogger.LogError("TopluHareketEkle error: " + ex);
            throw;
        }
    }


    public void HareketGuncelle(StokHareketi hareket)
    {
        StokHareketi normalized = NormalizeMovement(hareket);

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            StokHareketi mevcut = GetMovementById(connection, transaction, normalized.Id) ?? throw new InvalidOperationException(L("movement_not_found"));
            ValidateMovement(connection, transaction, normalized);

            double oldCardProjected = GetCurrentStockCore(connection, transaction, mevcut.StokKartId) - MovementImpact(mevcut);
            if (oldCardProjected < 0)
                throw new InvalidOperationException(L("stock_would_go_negative"));

            double newCardBase = mevcut.StokKartId == normalized.StokKartId
                ? oldCardProjected
                : GetCurrentStockCore(connection, transaction, normalized.StokKartId);
            double newCardProjected = newCardBase + MovementImpact(normalized);
            if (newCardProjected < 0)
                throw new InvalidOperationException(L("stock_would_go_negative"));

            using var command = CreateCommand(connection, transaction, @"
                UPDATE StokHareketleri
                SET
                    StokKartId=$sk,
                    Tur=$t,
                    Miktar=$m,
                    KimeVerildi=$kv,
                    Departman=$d,
                    Tarih=$ta,
                    Aciklama=$ac
                WHERE Id=$id");
            BindMovementParameters(command, normalized, includeId: true);

            if (command.ExecuteNonQuery() == 0)
                throw new InvalidOperationException(L("movement_not_found"));

            transaction.Commit();
        }
        catch (Exception ex)
        {
            AppLogger.LogError("HareketGuncelle error: " + ex);
            throw;
        }
    }


    public void HareketSil(int id)
    {
        TopluHareketSil(new[] { id });
    }
}
