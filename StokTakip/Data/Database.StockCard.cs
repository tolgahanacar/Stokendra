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

    public string SonrakiStokKodu()
    {
        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null,
                "SELECT MAX(CAST(KodNo AS INTEGER)) FROM StokKartlari WHERE KodNo GLOB '[0-9]*'");
            object? result = command.ExecuteScalar();
            int max = result != null && result != DBNull.Value
                ? Convert.ToInt32(result, CultureInfo.InvariantCulture)
                : 0;
            return (max + 1).ToString("D3", CultureInfo.InvariantCulture);
        }
        catch
        {
            return "001";
        }
    }


    public List<StokKarti> StokKartlariniGetir() => GetStockCards();

    public List<StokKarti> UstKartlariGetir() => GetStockCards("Ust");

    public List<StokKarti> AltKartlariGetir(int? ustId = null) => GetStockCards("Alt", ustId);


    private List<StokKarti> GetStockCards(string? kartTipi = null, int? ustKartId = null, int? id = null)
    {
        var liste = new List<StokKarti>();

        try
        {
            using var connection = CreateConnection();
            using var command = CreateCommand(connection, null, BuildStockCardQuery(kartTipi, ustKartId, id));

            if (!string.IsNullOrWhiteSpace(kartTipi))
                command.Parameters.AddWithValue("$kartTipi", kartTipi);
            if (ustKartId.HasValue)
                command.Parameters.AddWithValue("$ustKartId", ustKartId.Value);
            if (id.HasValue)
                command.Parameters.AddWithValue("$id", id.Value);

            using var reader = command.ExecuteReader();
            while (reader.Read())
                liste.Add(ReadStockCard(reader));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("StokKartlariniGetir error: " + ex);
        }

        return liste;
    }


    public StokKarti? StokKartiDetayGetir(int id)
    {
        return GetStockCards(id: id).FirstOrDefault();
    }


    public void StokKartiEkle(StokKarti stokKarti)
    {
        StokKarti normalized = NormalizeStokKarti(stokKarti);

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            ValidateStokKarti(connection, transaction, normalized, false);

            using var command = CreateCommand(connection, transaction, @"
                INSERT INTO StokKartlari
                    (Ad, KodNo, Aciklama, MinStok, Kategori, KartTipi, UstKartId, OlusturmaTarihi, Birim, Konum, Tedarikci, Barkod, BirimFiyat)
                VALUES
                    ($a, $k, $ac, $ms, $kat, $kt, $uk, $ot, $b, $kon, $ted, $bar, $bf)");
            BindStockCardParameters(command, normalized, includeId: false);
            command.Parameters.AddWithValue("$ot", DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();

            int newId = GetLastInsertRowId(connection, transaction);
            stokKarti.Id = newId;
            InsertAuditLog(connection, transaction, "EKLE", "StokKartlari", newId, $"{normalized.Ad} ({normalized.KodNo}) [{normalized.KartTipi}]");
            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("StokKartiEkle error: " + ex);
            throw;
        }
    }


    public void StokKartiGuncelle(StokKarti stokKarti)
    {
        StokKarti normalized = NormalizeStokKarti(stokKarti);

        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            EnsureStockCardExists(connection, transaction, normalized.Id);
            ValidateStokKarti(connection, transaction, normalized, true);

            using var command = CreateCommand(connection, transaction, @"
                UPDATE StokKartlari
                SET
                    Ad=$a,
                    KodNo=$k,
                    Aciklama=$ac,
                    MinStok=$ms,
                    Kategori=$kat,
                    KartTipi=$kt,
                    UstKartId=$uk,
                    GuncellenmeTarihi=$gt,
                    Birim=$b,
                    Konum=$kon,
                    Tedarikci=$ted,
                    Barkod=$bar,
                    BirimFiyat=$bf
                WHERE Id=$id");
            BindStockCardParameters(command, normalized, includeId: true);
            command.Parameters.AddWithValue("$gt", DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture));

            if (command.ExecuteNonQuery() == 0)
                throw new InvalidOperationException(L("record_not_found"));

            InsertAuditLog(connection, transaction, "GUNCELLE", "StokKartlari", normalized.Id, $"{normalized.Ad} ({normalized.KodNo})");
            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("StokKartiGuncelle error: " + ex);
            throw;
        }
    }


    public void StokKartiSil(int id)
    {
        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            StokKarti mevcut = GetStockCardById(connection, transaction, id) ?? throw new InvalidOperationException(L("record_not_found"));

            using (var detachChildren = CreateCommand(connection, transaction, "UPDATE StokKartlari SET UstKartId=NULL WHERE UstKartId=$id"))
            {
                detachChildren.Parameters.AddWithValue("$id", id);
                detachChildren.ExecuteNonQuery();
            }

            using (var deleteMovements = CreateCommand(connection, transaction, "DELETE FROM StokHareketleri WHERE StokKartId=$id"))
            {
                deleteMovements.Parameters.AddWithValue("$id", id);
                deleteMovements.ExecuteNonQuery();
            }

            using (var deleteCard = CreateCommand(connection, transaction, "DELETE FROM StokKartlari WHERE Id=$id"))
            {
                deleteCard.Parameters.AddWithValue("$id", id);
                if (deleteCard.ExecuteNonQuery() == 0)
                    throw new InvalidOperationException(L("record_not_found"));
            }

            InsertAuditLog(connection, transaction, "SIL", "StokKartlari", id, $"{mevcut.Ad} ({mevcut.KodNo})");
            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("StokKartiSil error: " + ex);
            throw;
        }
    }


    private static void BindStockCardParameters(SqliteCommand command, StokKarti stokKarti, bool includeId)
    {
        command.Parameters.AddWithValue("$a", stokKarti.Ad);
        command.Parameters.AddWithValue("$k", stokKarti.KodNo);
        command.Parameters.AddWithValue("$ac", stokKarti.Aciklama);
        command.Parameters.AddWithValue("$ms", stokKarti.MinStok);
        command.Parameters.AddWithValue("$kat", stokKarti.Kategori);
        command.Parameters.AddWithValue("$kt", stokKarti.KartTipi);
        command.Parameters.AddWithValue("$uk", stokKarti.UstKartId.HasValue ? stokKarti.UstKartId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$b", stokKarti.Birim);
        command.Parameters.AddWithValue("$kon", stokKarti.Konum);
        command.Parameters.AddWithValue("$ted", stokKarti.Tedarikci);
        command.Parameters.AddWithValue("$bar", stokKarti.Barkod);
        command.Parameters.AddWithValue("$bf", stokKarti.BirimFiyat);

        if (includeId)
            command.Parameters.AddWithValue("$id", stokKarti.Id);
    }


    private static StokKarti ReadStockCard(SqliteDataReader reader)
    {
        return new StokKarti
        {
            Id = reader.GetInt32(0),
            Ad = reader.GetString(1),
            KodNo = reader.GetString(2),
            Aciklama = reader.IsDBNull(3) ? "" : reader.GetString(3),
            MinStok = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
            MevcutStok = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
            Kategori = reader.IsDBNull(6) ? "" : reader.GetString(6),
            KartTipi = reader.IsDBNull(7) ? "Alt" : reader.GetString(7),
            UstKartId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            UstKartAd = reader.IsDBNull(9) ? "" : reader.GetString(9),
            Birim = reader.IsDBNull(10) ? "Adet" : reader.GetString(10),
            Konum = reader.IsDBNull(11) ? "" : reader.GetString(11),
            Tedarikci = reader.IsDBNull(12) ? "" : reader.GetString(12),
            Barkod = reader.IsDBNull(13) ? "" : reader.GetString(13),
            BirimFiyat = reader.IsDBNull(14) ? 0 : reader.GetDouble(14)
        };
    }


    private static string BuildStockCardQuery(string? kartTipi, int? ustKartId, int? id)
    {
        var conditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(kartTipi))
            conditions.Add("COALESCE(s.KartTipi, 'Alt') = $kartTipi");
        if (ustKartId.HasValue)
            conditions.Add("s.UstKartId = $ustKartId");
        if (id.HasValue)
            conditions.Add("s.Id = $id");

        string whereClause = conditions.Count == 0 ? "" : "WHERE " + string.Join(" AND ", conditions);

        return $@"
            WITH hareket_ozet AS (
                SELECT
                    StokKartId,
                    SUM(CASE WHEN Tur='Giris' THEN Miktar WHEN Tur='Cikis' THEN -Miktar ELSE 0 END) AS MevcutStok
                FROM StokHareketleri
                GROUP BY StokKartId
            )
            SELECT
                s.Id,
                s.Ad,
                s.KodNo,
                s.Aciklama,
                s.MinStok,
                COALESCE(h.MevcutStok, 0),
                s.Kategori,
                COALESCE(s.KartTipi, 'Alt'),
                s.UstKartId,
                COALESCE(ust.Ad, ''),
                s.Birim,
                s.Konum,
                s.Tedarikci,
                s.Barkod,
                s.BirimFiyat
            FROM StokKartlari s
            LEFT JOIN StokKartlari ust ON ust.Id = s.UstKartId
            LEFT JOIN hareket_ozet h ON h.StokKartId = s.Id
            {whereClause}
            ORDER BY s.KodNo COLLATE NOCASE, s.Id";
    }


    private static StokKarti NormalizeStokKarti(StokKarti stokKarti)
    {
        string kartTipi = TrimTo(stokKarti.KartTipi, 10);
        if (!ValidCardTypes.Contains(kartTipi))
            kartTipi = "Alt";

        return new StokKarti
        {
            Id = stokKarti.Id,
            Ad = NormalizeRequiredText(stokKarti.Ad, 150, L("name_required")),
            KodNo = NormalizeRequiredText(stokKarti.KodNo, 80, L("code_required")).ToUpperInvariant(),
            Aciklama = TrimTo(stokKarti.Aciklama, 500),
            MinStok = Math.Max(0, stokKarti.MinStok),
            Kategori = TrimTo(stokKarti.Kategori, 120),
            KartTipi = kartTipi,
            UstKartId = kartTipi == "Alt" ? stokKarti.UstKartId : null,
            Birim = string.IsNullOrWhiteSpace(stokKarti.Birim) ? "Adet" : TrimTo(stokKarti.Birim, 40),
            Konum = TrimTo(stokKarti.Konum, 150),
            Tedarikci = TrimTo(stokKarti.Tedarikci, 150),
            Barkod = TrimTo(stokKarti.Barkod, 80),
            BirimFiyat = stokKarti.BirimFiyat < 0 ? 0 : stokKarti.BirimFiyat
        };
    }


    private static void ValidateStokKarti(SqliteConnection connection, SqliteTransaction transaction, StokKarti stokKarti, bool isUpdate)
    {
        if (stokKarti.KartTipi == "Alt" && stokKarti.UstKartId == stokKarti.Id && stokKarti.Id > 0)
            throw new InvalidOperationException(L("parent_card_self"));

        if (stokKarti.KartTipi == "Alt" && stokKarti.UstKartId.HasValue)
        {
            using var parentCommand = CreateCommand(connection, transaction,
                "SELECT COUNT(*) FROM StokKartlari WHERE Id=$id AND COALESCE(KartTipi,'Alt')='Ust'");
            parentCommand.Parameters.AddWithValue("$id", stokKarti.UstKartId.Value);
            if (Convert.ToInt64(parentCommand.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) == 0)
                throw new InvalidOperationException(L("parent_card_not_found"));

            if (stokKarti.Id > 0)
                EnsureNoHierarchyLoop(connection, transaction, stokKarti.Id, stokKarti.UstKartId.Value);
        }

        if (stokKarti.KartTipi == "Ust" && isUpdate && HasMovements(connection, transaction, stokKarti.Id))
            throw new InvalidOperationException(L("parent_card_with_movements"));

        using var duplicateCode = CreateCommand(connection, transaction,
            "SELECT COUNT(*) FROM StokKartlari WHERE KodNo=$kod COLLATE NOCASE AND Id<>$id");
        duplicateCode.Parameters.AddWithValue("$kod", stokKarti.KodNo);
        duplicateCode.Parameters.AddWithValue("$id", stokKarti.Id);
        if (Convert.ToInt64(duplicateCode.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) > 0)
            throw new InvalidOperationException(L("stock_code_exists"));
    }


    private static void EnsureStockCardExists(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var command = CreateCommand(connection, transaction, "SELECT COUNT(*) FROM StokKartlari WHERE Id=$id");
        command.Parameters.AddWithValue("$id", id);
        if (Convert.ToInt64(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) == 0)
            throw new InvalidOperationException(L("record_not_found"));
    }


    private static void EnsureNoHierarchyLoop(SqliteConnection connection, SqliteTransaction transaction, int cardId, int parentId)
    {
        var visited = new HashSet<int>();
        int? current = parentId;

        while (current.HasValue)
        {
            if (!visited.Add(current.Value) || current.Value == cardId)
                throw new InvalidOperationException(L("parent_card_loop"));

            using var command = CreateCommand(connection, transaction, "SELECT UstKartId FROM StokKartlari WHERE Id=$id");
            command.Parameters.AddWithValue("$id", current.Value);
            object? value = command.ExecuteScalar();
            current = value == null || value == DBNull.Value
                ? null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }


    private static StokKarti? GetStockCardById(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var command = CreateCommand(connection, transaction, BuildStockCardQuery(null, null, id));
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadStockCard(reader) : null;
    }
}
