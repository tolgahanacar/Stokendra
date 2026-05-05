using System.Globalization;
using Microsoft.Data.Sqlite;
using StokTakip.Models;
using static StokTakip.LocalizationManager;

namespace StokTakip.Data;

public sealed partial class Database
{
    private static StokHareketi NormalizeMovement(StokHareketi hareket)
    {
        string tur = TrimTo(hareket.Tur, 10);
        if (!ValidMovementTypes.Contains(tur))
            throw new InvalidOperationException(L("movement_type_invalid"));

        double miktar = tur == "Bos" ? 0 : hareket.Miktar;

        return new StokHareketi
        {
            Id = hareket.Id,
            StokKartId = hareket.StokKartId,
            Tur = tur,
            Miktar = Math.Round(miktar, 4, MidpointRounding.AwayFromZero),
            TeslimEdilen = TrimTo(hareket.TeslimEdilen, 150),
            Departman = NormalizeRequiredText(hareket.Departman, 120, L("select_dept")),
            Tarih = hareket.Tarih == default ? DateTime.Now : hareket.Tarih,
            Aciklama = TrimTo(hareket.Aciklama, 500)
        };
    }

    private static ServisKaydi NormalizeServiceRecord(ServisKaydi kayit)
    {
        return new ServisKaydi
        {
            Id = kayit.Id,
            CihazAdi = NormalizeRequiredText(kayit.CihazAdi, 150, L("device_name_required")),
            SeriNumarasi = TrimTo(kayit.SeriNumarasi, 120),
            Firma = TrimTo(kayit.Firma, 150),
            BakimTarihi = kayit.BakimTarihi == default ? DateTime.Now : kayit.BakimTarihi,
            Sorun = TrimTo(kayit.Sorun, 500),
            Sonuc = TrimTo(kayit.Sonuc, 500)
        };
    }

    private static void ValidateMovement(SqliteConnection connection, SqliteTransaction transaction, StokHareketi hareket)
    {
        if (!ValidMovementTypes.Contains(hareket.Tur))
            throw new InvalidOperationException(L("movement_type_invalid"));
        if (hareket.StokKartId <= 0)
            throw new InvalidOperationException(L("select_stock_card"));
        if (hareket.Tur != "Bos" && hareket.Miktar <= 0)
            throw new InvalidOperationException(L("enter_valid_qty"));
        if (hareket.Tur == "Bos")
            hareket.Miktar = 0;

        using var cardCommand = CreateCommand(connection, transaction,
            "SELECT COALESCE(KartTipi,'Alt') FROM StokKartlari WHERE Id=$id");
        cardCommand.Parameters.AddWithValue("$id", hareket.StokKartId);
        string? cardType = cardCommand.ExecuteScalar()?.ToString();

        if (cardType == null)
            throw new InvalidOperationException(L("card_not_found"));
        if (!string.Equals(cardType, "Alt", StringComparison.Ordinal))
            throw new InvalidOperationException(L("movement_only_child_cards"));
    }

    private static void ValidateServiceRecord(ServisKaydi kayit)
    {
        if (string.IsNullOrWhiteSpace(kayit.CihazAdi))
            throw new InvalidOperationException(L("device_name_required"));
    }

    private static bool HasMovements(SqliteConnection connection, SqliteTransaction transaction, int stockCardId)
    {
        using var command = CreateCommand(connection, transaction, "SELECT COUNT(*) FROM StokHareketleri WHERE StokKartId=$id");
        command.Parameters.AddWithValue("$id", stockCardId);
        return Convert.ToInt64(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture) > 0;
    }

    private static double GetCurrentStockCore(SqliteConnection connection, SqliteTransaction transaction, int stockCardId)
    {
        using var command = CreateCommand(connection, transaction, @"
            SELECT COALESCE(SUM(
                CASE
                    WHEN Tur='Giris' THEN Miktar
                    WHEN Tur='Cikis' THEN -Miktar
                    ELSE 0
                END
            ), 0)
            FROM StokHareketleri
            WHERE StokKartId=$id");
        command.Parameters.AddWithValue("$id", stockCardId);
        return Convert.ToDouble(command.ExecuteScalar() ?? 0d, CultureInfo.InvariantCulture);
    }

    private static void EnsureMovementWillNotCreateNegativeStock(SqliteConnection connection, SqliteTransaction transaction, int stockCardId, double impact)
    {
        double projected = GetCurrentStockCore(connection, transaction, stockCardId) + impact;
        if (projected < 0)
            throw new InvalidOperationException(L("stock_would_go_negative"));
    }

    private static double MovementImpact(StokHareketi hareket)
    {
        return hareket.Tur switch
        {
            "Giris" => hareket.Miktar,
            "Cikis" => -hareket.Miktar,
            _ => 0
        };
    }

    private static StokHareketi? GetMovementById(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var command = CreateCommand(connection, transaction, @"
            SELECT
                h.Id,
                h.StokKartId,
                s.Ad,
                s.KodNo,
                h.Tur,
                h.Miktar,
                h.KimeVerildi,
                h.Departman,
                h.Tarih,
                h.Aciklama
            FROM StokHareketleri h
            JOIN StokKartlari s ON s.Id = h.StokKartId
            WHERE h.Id=$id");
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadMovement(reader) : null;
    }

    private static ServisKaydi? GetServisKaydiById(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var command = CreateCommand(connection, transaction, @"
            SELECT Id, CihazAdi, SeriNumarasi, Firma, BakimTarihi, Sorun, Sonuc
            FROM ServisKayitlari
            WHERE Id=$id");
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        return new ServisKaydi
        {
            Id = reader.GetInt32(0),
            CihazAdi = reader.GetString(1),
            SeriNumarasi = reader.IsDBNull(2) ? "" : reader.GetString(2),
            Firma = reader.IsDBNull(3) ? "" : reader.GetString(3),
            BakimTarihi = ParseDateSafe(reader.GetString(4)),
            Sorun = reader.IsDBNull(5) ? "" : reader.GetString(5),
            Sonuc = reader.IsDBNull(6) ? "" : reader.GetString(6)
        };
    }
}

