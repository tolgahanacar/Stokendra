using System;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace StokTakip.Data;

public sealed partial class Database
{
    private static string NormalizeRequiredText(string? value, int maxLength, string errorMessage)
    {
        string normalized = TrimTo(value, maxLength);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException(errorMessage);
        return normalized;
    }

    private static string TrimTo(string? value, int maxLength)
    {
        string normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string ToSqlLiteral(object? value)
    {
        if (value == null || value == DBNull.Value)
            return "NULL";

        return value switch
        {
            byte[] bytes => "X'" + Convert.ToHexString(bytes) + "'",
            string text => $"'{text.Replace("'", "''")}'",
            bool flag => flag ? "1" : "0",
            float or double or decimal => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
            sbyte or byte or short or ushort or int or uint or long or ulong => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
            DateTime dateTime => $"'{dateTime.ToString(DateFormat, CultureInfo.InvariantCulture)}'",
            _ => $"'{value.ToString()?.Replace("'", "''")}'"
        };
    }

    private static DateTime ParseDateSafe(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return DateTime.MinValue;

        string[] formats =
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd",
            "dd.MM.yyyy HH:mm:ss",
            "dd.MM.yyyy HH:mm",
            "dd.MM.yyyy",
            "MM/dd/yyyy HH:mm:ss",
            "MM/dd/yyyy",
            "O",
            "o"
        };

        if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            return exact;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;

        return DateTime.MinValue;
    }

    private static int GetLastInsertRowId(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = CreateCommand(connection, transaction, "SELECT last_insert_rowid();");
        return Convert.ToInt32(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
    }
}

