using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.RegularExpressions;

namespace StokTakip.Data;

public static class SqliteHelpers
{
    public static void RegisterCustomFunctions(SqliteConnection connection)
    {
        connection.CreateFunction("like", (string pattern, string input) => SqlLike(pattern, input));
    }

    private static bool SqlLike(string pattern, string input)
    {
        if (input == null || pattern == null) return false;

        var culture = new CultureInfo("tr-TR");
        string inputLower = input.ToLower(culture);
        string patternLower = pattern.ToLower(culture);

        // Optimize standard %term% matches
        if (patternLower.StartsWith("%") && patternLower.EndsWith("%") && patternLower.Length >= 2)
        {
            string term = patternLower.Substring(1, patternLower.Length - 2);
            if (!term.Contains("%") && !term.Contains("_"))
            {
                return inputLower.Contains(term);
            }
        }

        // General SQL LIKE pattern matching via Regex
        string regexPattern = "^" + Regex.Escape(patternLower)
            .Replace("\\%", ".*")
            .Replace("\\_", ".") + "$";

        try
        {
            return Regex.IsMatch(inputLower, regexPattern);
        }
        catch
        {
            return false;
        }
    }
}
