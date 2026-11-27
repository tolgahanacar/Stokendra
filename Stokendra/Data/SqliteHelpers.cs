using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System;

namespace Stokendra.Data;

public static class SqliteHelpers
{
    private static readonly CultureInfo TurkishCulture = new("tr-TR");
    private static readonly CompareInfo TurkishCompare = TurkishCulture.CompareInfo;
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new(StringComparer.Ordinal);

    private enum LikeMatchMode
    {
        Exact = 0,
        Contains = 1,
        StartsWith = 2,
        EndsWith = 3,
        Regex = 4
    }

    [ThreadStatic]
    private static string? _lastPattern;
    [ThreadStatic]
    private static LikeMatchMode _lastMode;
    [ThreadStatic]
    private static string? _lastTerm;
    [ThreadStatic]
    private static Regex? _lastRegex;

    public static void RegisterCustomFunctions(SqliteConnection connection)
    {
        connection.CreateFunction("like", (string pattern, string input) => SqlLike(pattern, input));
    }

    private static bool SqlLike(string pattern, string input)
    {
        if (input == null || pattern == null) return false;

        if (!string.Equals(pattern, _lastPattern, StringComparison.Ordinal))
        {
            _lastPattern = pattern;

            if (pattern.Length >= 2 && pattern.StartsWith('%') && pattern.EndsWith('%'))
            {
                string term = pattern.Substring(1, pattern.Length - 2);
                if (!term.Contains('%') && !term.Contains('_'))
                {
                    _lastMode = LikeMatchMode.Contains;
                    _lastTerm = term;
                    _lastRegex = null;
                }
                else
                {
                    CompileRegex(pattern);
                }
            }
            else if (pattern.Length >= 2 && !pattern.StartsWith('%') && pattern.EndsWith('%'))
            {
                string term = pattern.Substring(0, pattern.Length - 1);
                if (!term.Contains('%') && !term.Contains('_'))
                {
                    _lastMode = LikeMatchMode.StartsWith;
                    _lastTerm = term;
                    _lastRegex = null;
                }
                else
                {
                    CompileRegex(pattern);
                }
            }
            else if (pattern.Length >= 2 && pattern.StartsWith('%') && !pattern.EndsWith('%'))
            {
                string term = pattern.Substring(1);
                if (!term.Contains('%') && !term.Contains('_'))
                {
                    _lastMode = LikeMatchMode.EndsWith;
                    _lastTerm = term;
                    _lastRegex = null;
                }
                else
                {
                    CompileRegex(pattern);
                }
            }
            else if (!pattern.Contains('%') && !pattern.Contains('_'))
            {
                _lastMode = LikeMatchMode.Exact;
                _lastTerm = pattern;
                _lastRegex = null;
            }
            else
            {
                CompileRegex(pattern);
            }
        }

        return _lastMode switch
        {
            LikeMatchMode.Contains => TurkishCompare.IndexOf(input, _lastTerm!, CompareOptions.IgnoreCase) >= 0,
            LikeMatchMode.StartsWith => TurkishCompare.IsPrefix(input, _lastTerm!, CompareOptions.IgnoreCase),
            LikeMatchMode.EndsWith => TurkishCompare.IsSuffix(input, _lastTerm!, CompareOptions.IgnoreCase),
            LikeMatchMode.Exact => TurkishCompare.Compare(input, _lastTerm!, CompareOptions.IgnoreCase) == 0,
            _ => MatchRegex(input)
        };
    }

    private static void CompileRegex(string pattern)
    {
        _lastMode = LikeMatchMode.Regex;
        _lastTerm = pattern.ToLower(TurkishCulture);
        string regexPattern = "^" + Regex.Escape(_lastTerm)
            .Replace("\\%", ".*")
            .Replace("\\_", ".") + "$";

        if (RegexCache.Count > 500)
            RegexCache.Clear();

        _lastRegex = RegexCache.GetOrAdd(regexPattern, pat => new Regex(pat, RegexOptions.Singleline));
    }

    private static bool MatchRegex(string input)
    {
        try
        {
            return _lastRegex != null && _lastRegex.IsMatch(input.ToLower(TurkishCulture));
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
            PRAGMA busy_timeout    = 5000;
            PRAGMA synchronous     = NORMAL;
            PRAGMA temp_store      = MEMORY;
            PRAGMA cache_size      = -16384;
            PRAGMA mmap_size       = 67108864;
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
