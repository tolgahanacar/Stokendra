using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Stokendra.Infrastructure;

/// <summary>
/// ObservableCollection subclass that supports bulk range additions.
/// Suppresses CollectionChanged notifications until all items are added,
/// then fires a single Reset notification to optimize UI rendering performance.
/// </summary>
public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification = false;

    public BulkObservableCollection() : base() { }

    public BulkObservableCollection(IEnumerable<T> collection) : base(collection) { }

    public BulkObservableCollection(List<T> list) : base(list) { }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
            base.OnCollectionChanged(e);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!_suppressNotification)
            base.OnPropertyChanged(e);
    }

    /// <summary>
    /// Adds a range of items to the collection and raises a single Reset notification.
    /// </summary>
    public void AddRange(IEnumerable<T> range)
    {
        if (range == null) throw new ArgumentNullException(nameof(range));

        _suppressNotification = true;
        try
        {
            foreach (var item in range)
            {
                Items.Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
