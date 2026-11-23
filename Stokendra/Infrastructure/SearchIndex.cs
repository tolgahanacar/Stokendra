using System;
using System.Collections.Generic;
using System.Linq;

namespace Stokendra.Infrastructure;

/// <summary>
/// High-performance in-memory prefix and token search index.
/// Tokenizes items by delimiters, normalizes them using Turkish culture,
/// and pre-computes prefix lookup tables (1-char to 32-char prefixes)
/// allowing instant O(1) character-by-character search lookups.
/// </summary>
/// <typeparam name="T">Entity type being indexed.</typeparam>
public class SearchIndex<T> where T : class
{
    private readonly Func<T, IEnumerable<string?>> _tokensExtractor;
    private readonly Func<T, string, bool> _fallbackMatcher;
    private readonly object _syncLock = new();

    private IReadOnlyList<T> _allItems = Array.Empty<T>();
    private Dictionary<string, List<T>> _prefixMap = new(StringComparer.Ordinal);

    private static readonly char[] Delimiters = { ' ', '-', '_', '/', '.', ',', ';', ':', '(', ')', '[', ']', '{', '}', '+', '*' };

    public SearchIndex(
        Func<T, IEnumerable<string?>> tokensExtractor,
        Func<T, string, bool>? fallbackMatcher = null)
    {
        _tokensExtractor = tokensExtractor ?? throw new ArgumentNullException(nameof(tokensExtractor));
        _fallbackMatcher = fallbackMatcher ?? ((_, _) => true);
    }

    /// <summary>
    /// Total number of indexed items.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_syncLock)
            {
                return _allItems.Count;
            }
        }
    }

    /// <summary>
    /// Total number of unique prefix entries in memory.
    /// </summary>
    public int PrefixCount
    {
        get
        {
            lock (_syncLock)
            {
                return _prefixMap.Count;
            }
        }
    }

    /// <summary>
    /// Builds or rebuilds the in-memory prefix index over the provided items.
    /// Preserves original item order within each prefix bucket.
    /// </summary>
    public void Build(IEnumerable<T>? items)
    {
        var list = items?.ToList() ?? new List<T>();
        var newPrefixMap = new Dictionary<string, List<T>>(StringComparer.Ordinal);
        var itemPrefixes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in list)
        {
            itemPrefixes.Clear();
            var tokens = _tokensExtractor(item);
            if (tokens != null)
            {
                foreach (var rawToken in tokens)
                {
                    if (string.IsNullOrWhiteSpace(rawToken)) continue;

                    // Index entire raw token prefix
                    IndexToken(rawToken, itemPrefixes);

                    // Split into sub-words by delimiters
                    var parts = rawToken.Split(Delimiters, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        IndexToken(part, itemPrefixes);
                    }
                }
            }

            foreach (var prefix in itemPrefixes)
            {
                if (!newPrefixMap.TryGetValue(prefix, out var bucket))
                {
                    bucket = new List<T>();
                    newPrefixMap[prefix] = bucket;
                }
                bucket.Add(item);
            }
        }

        lock (_syncLock)
        {
            _allItems = list;
            _prefixMap = newPrefixMap;
        }
    }

    private static void IndexToken(string token, HashSet<string> prefixes)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        var normalized = token.Trim().ToTurkishLower();
        int maxLen = Math.Min(normalized.Length, 32);
        for (int i = 1; i <= maxLen; i++)
        {
            prefixes.Add(normalized.Substring(0, i));
        }
    }

    /// <summary>
    /// Searches for items matching all space-separated tokens in the query.
    /// Single token search executes in O(1) time directly from the prefix map.
    /// Multi-token search performs fast set intersection.
    /// Fallback matcher handles arbitrary non-prefix substring queries.
    /// </summary>
    public IReadOnlyList<T> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            lock (_syncLock)
            {
                return _allItems;
            }
        }

        var normalizedQuery = query.Trim().ToTurkishLower();
        var searchTerms = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (searchTerms.Length == 0)
        {
            lock (_syncLock)
            {
                return _allItems;
            }
        }

        Dictionary<string, List<T>> prefixMap;
        IReadOnlyList<T> allItems;
        lock (_syncLock)
        {
            prefixMap = _prefixMap;
            allItems = _allItems;
        }

        // Single term search - O(1) direct dictionary lookup
        if (searchTerms.Length == 1)
        {
            var term = searchTerms[0];
            if (prefixMap.TryGetValue(term, out var list))
            {
                return list;
            }

            // Fallback for non-prefix substring or arbitrary middle-word search
            return allItems.Where(item => _fallbackMatcher(item, term)).ToList();
        }

        // Multi-term search: find prefix matches for each term
        List<T>? smallestBucket = null;
        var otherBuckets = new List<IReadOnlyCollection<T>>();
        var nonIndexedTerms = new List<string>();

        foreach (var term in searchTerms)
        {
            if (prefixMap.TryGetValue(term, out var bucket))
            {
                if (bucket.Count == 0) return Array.Empty<T>();

                if (smallestBucket == null || bucket.Count < smallestBucket.Count)
                {
                    if (smallestBucket != null) otherBuckets.Add(smallestBucket);
                    smallestBucket = bucket;
                }
                else
                {
                    otherBuckets.Add(bucket);
                }
            }
            else
            {
                nonIndexedTerms.Add(term);
            }
        }

        // If at least one term had a prefix bucket:
        if (smallestBucket != null)
        {
            var filterSets = otherBuckets.Select(b => new HashSet<T>(b)).ToList();

            var results = new List<T>(smallestBucket.Count);
            foreach (var item in smallestBucket)
            {
                bool matchesAll = true;
                foreach (var filterSet in filterSets)
                {
                    if (!filterSet.Contains(item))
                    {
                        matchesAll = false;
                        break;
                    }
                }

                if (matchesAll && nonIndexedTerms.Count > 0)
                {
                    foreach (var term in nonIndexedTerms)
                    {
                        if (!_fallbackMatcher(item, term))
                        {
                            matchesAll = false;
                            break;
                        }
                    }
                }

                if (matchesAll)
                {
                    results.Add(item);
                }
            }
            return results;
        }

        // Fallback when none of the terms matched any prefix
        return allItems.Where(item => searchTerms.All(t => _fallbackMatcher(item, t))).ToList();
    }
}
