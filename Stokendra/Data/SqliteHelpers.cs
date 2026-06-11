using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace Stokendra.Data;

public static class SqliteHelpers
{
    private static readonly CultureInfo TurkishCulture = new("tr-TR");
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new(StringComparer.Ordinal);

    public static void RegisterCustomFunctions(SqliteConnection connection)
    {
        connection.CreateFunction("like", (string pattern, string input) => SqlLike(pattern, input));
    }

    private static bool SqlLike(string pattern, string input)
    {
        if (input == null || pattern == null) return false;

        string inputLower = input.ToLower(TurkishCulture);
        string patternLower = pattern.ToLower(TurkishCulture);

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
            var regex = RegexCache.GetOrAdd(regexPattern, pat => new Regex(pat, RegexOptions.Singleline));
            return regex.IsMatch(inputLower);
        }
        catch
        {
            return false;
        }
    }
}
