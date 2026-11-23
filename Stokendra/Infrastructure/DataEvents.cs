using System;

namespace Stokendra.Infrastructure;

/// <summary>
/// Domain categories for data change notifications across ViewModels.
/// </summary>
public enum DataDomain
{
    StockCards,
    Movements,
    Departments,
    Users,
    Notes,
    Services
}

/// <summary>
/// Lightweight, decoupled in-memory event bus for coordinating cache invalidation
/// across ViewModels without tight couplings or direct references.
/// </summary>
public static class DataEvents
{
    public static event Action<DataDomain>? DataChanged;

    /// <summary>
    /// Broadcasts a data mutation event to invalidate relevant ViewModel caches.
    /// </summary>
    public static void NotifyChanged(DataDomain domain)
    {
        try
        {
            DataChanged?.Invoke(domain);
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"DataEvents: Error notifying change for {domain}", ex);
        }
    }
}
