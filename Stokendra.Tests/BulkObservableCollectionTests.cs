using Xunit;
using Stokendra.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Stokendra.Tests;

public class BulkObservableCollectionTests
{
    [Fact]
    public void ReplaceAll_ReplacesItems_And_FiresSingleReset()
    {
        // Arrange
        var collection = new BulkObservableCollection<string>(new[] { "Old1", "Old2", "Old3" });
        int resetEventCount = 0;
        int otherEventCount = 0;

        collection.CollectionChanged += (sender, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
                resetEventCount++;
            else
                otherEventCount++;
        };

        var newItems = new List<string> { "New1", "New2", "New3", "New4" };

        // Act
        collection.ReplaceAll(newItems);

        // Assert
        Assert.Equal(4, collection.Count);
        Assert.Equal("New1", collection[0]);
        Assert.Equal("New4", collection[3]);
        Assert.Equal(1, resetEventCount);
        Assert.Equal(0, otherEventCount);
    }

    [Fact]
    public void ReplaceAll_WithEmpty_ClearsCollection()
    {
        // Arrange
        var collection = new BulkObservableCollection<int>(new[] { 1, 2, 3 });
        int resetEventCount = 0;

        collection.CollectionChanged += (sender, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
                resetEventCount++;
        };

        // Act
        collection.ReplaceAll(Array.Empty<int>());

        // Assert
        Assert.Empty(collection);
        Assert.Equal(1, resetEventCount);
    }

    [Fact]
    public void ReplaceAll_Null_ThrowsArgumentNullException()
    {
        var collection = new BulkObservableCollection<string>();
        Assert.Throws<ArgumentNullException>(() => collection.ReplaceAll(null!));
    }
}
