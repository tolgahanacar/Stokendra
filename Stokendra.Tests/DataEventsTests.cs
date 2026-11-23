using Stokendra.Infrastructure;
using System;
using Xunit;

namespace Stokendra.Tests;

public class DataEventsTests
{
    [Fact]
    public void Test_DataEvents_NotifyChanged_TriggersSubscribers()
    {
        DataDomain? notifiedDomain = null;
        int callCount = 0;

        void Handler(DataDomain domain)
        {
            notifiedDomain = domain;
            callCount++;
        }

        DataEvents.DataChanged += Handler;

        try
        {
            DataEvents.NotifyChanged(DataDomain.StockCards);
            Assert.Equal(DataDomain.StockCards, notifiedDomain);
            Assert.Equal(1, callCount);

            DataEvents.NotifyChanged(DataDomain.Movements);
            Assert.Equal(DataDomain.Movements, notifiedDomain);
            Assert.Equal(2, callCount);
        }
        finally
        {
            DataEvents.DataChanged -= Handler;
        }
    }

    [Fact]
    public void Test_DataEvents_ExceptionInSubscriber_DoesNotThrow()
    {
        void FaultyHandler(DataDomain domain)
        {
            throw new InvalidOperationException("Subscriber test error");
        }

        DataEvents.DataChanged += FaultyHandler;

        try
        {
            var ex = Record.Exception(() => DataEvents.NotifyChanged(DataDomain.Departments));
            Assert.Null(ex);
        }
        finally
        {
            DataEvents.DataChanged -= FaultyHandler;
        }
    }
}
