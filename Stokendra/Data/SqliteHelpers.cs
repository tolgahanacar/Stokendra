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
            Regex regex;
            if (regexPattern == _lastRegexPattern && _lastRegex != null)
            {
                regex = _lastRegex;
            }
            else
            {
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
    /// Applies standard SQLite performance pragmas to a connection.
    /// Centralized to avoid duplication between Database and SqliteConnectionFactory.
    /// </summary>
    public static void ApplyConnectionPragmas(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            PRAGMA foreign_keys    = ON;
            PRAGMA busy_timeout    = 30000;
            PRAGMA synchronous     = NORMAL;
            PRAGMA journal_mode    = WAL;
            PRAGMA temp_store      = MEMORY;
            PRAGMA cache_size      = -131072;
            PRAGMA mmap_size       = 268435456;
        ";
        cmd.ExecuteNonQuery();
    }
}
