using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System;

namespace Stokendra.Data;

public static class SqliteHelpers
{
    private static readonly CultureInfo TurkishCulture = new("tr-TR");
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new(StringComparer.Ordinal);

    [ThreadStatic]
    private static string? _lastPattern;
    [ThreadStatic]
    private static string? _lastPatternLower;
    [ThreadStatic]
    private static string? _lastRegexPattern;
    [ThreadStatic]
    private static Regex? _lastRegex;

    public static void RegisterCustomFunctions(SqliteConnection connection)
    {
        connection.CreateFunction("like", (string pattern, string input) => SqlLike(pattern, input));
    }

    private static bool SqlLike(string pattern, string input)
    {
        if (input == null || pattern == null) return false;

        // Caching lowercased pattern using thread-local fields
        string patternLower;
        if (pattern == _lastPattern)
        {
            patternLower = _lastPatternLower!;
        }
        else
        {
            patternLower = pattern.ToLower(TurkishCulture);
            _lastPattern = pattern;
            _lastPatternLower = patternLower;
        }

        string inputLower = input.ToLower(TurkishCulture);

        // 1. Optimize %term% (contains)
        if (patternLower.StartsWith("%") && patternLower.EndsWith("%") && patternLower.Length >= 2)
        {
            string term = patternLower.Substring(1, patternLower.Length - 2);
            if (!term.Contains("%") && !term.Contains("_"))
            {
                return inputLower.Contains(term);
            }
        }

        // 2. Optimize term% (starts with)
        if (!patternLower.StartsWith("%") && patternLower.EndsWith("%") && patternLower.Length >= 2)
        {
            string term = patternLower.Substring(0, patternLower.Length - 1);
            if (!term.Contains("%") && !term.Contains("_"))
            {
                return inputLower.StartsWith(term);
            }
        }

        // 3. Optimize %term (ends with)
        if (patternLower.StartsWith("%") && !patternLower.EndsWith("%") && patternLower.Length >= 2)
        {
            string term = patternLower.Substring(1);
            if (!term.Contains("%") && !term.Contains("_"))
            {
                return inputLower.EndsWith(term);
            }
        }

        // 4. Optimize exact term (no wildcards)
        if (!patternLower.Contains("%") && !patternLower.Contains("_"))
        {
            return inputLower == patternLower;
        }

        // 5. General SQL LIKE pattern matching via Regex
        string regexPattern = "^" + Regex.Escape(patternLower)
            .Replace("\\%", ".*")
            .Replace("\\_", ".") + "$";

        try
        {
            Regex regex;
            if (regexPattern == _lastRegexPattern && _lastRegex != null)
            {
                regex = _lastRegex;
            }
            else
            {
                if (RegexCache.Count > 500)
                    RegexCache.Clear();

                regex = RegexCache.GetOrAdd(regexPattern, pat => new Regex(pat, RegexOptions.Singleline));
                _lastRegexPattern = regexPattern;
                _lastRegex = regex;
            }
            return regex.IsMatch(inputLower);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Applies connection-level SQLite performance pragmas.
    /// Excludes journal_mode which is a persistent database-level property.
    /// </summary>
    public static void ApplyConnectionPragmas(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            PRAGMA synchronous     = NORMAL;
            PRAGMA temp_store      = MEMORY;
            PRAGMA cache_size      = -131072;
            PRAGMA mmap_size       = 268435456;
        ";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Ensures database-level persistent pragmas (WAL mode) on database initialization.
    /// </summary>
    public static void EnsureDatabasePragmas(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous  = NORMAL;
        ";
        cmd.ExecuteNonQuery();
    }
}
