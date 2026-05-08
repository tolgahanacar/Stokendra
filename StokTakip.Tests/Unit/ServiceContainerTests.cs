using Microsoft.Extensions.DependencyInjection;
using StokTakip.Infrastructure;

namespace StokTakip.Tests.Unit;

/// <summary>
/// ServiceContainer (static IServiceProvider wrapper) için unit testler.
/// </summary>
public class ServiceContainerTests
{
    [Fact]
    public void GetService_RegisteredType_ReturnsInstance()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var provider = services.BuildServiceProvider();
        ServiceContainer.Initialize(provider);

        var result = ServiceContainer.GetService<ITestService>();

        Assert.NotNull(result);
        Assert.IsType<TestService>(result);
    }

    [Fact]
    public void GetService_Singleton_SameInstanceReturnedTwice()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var provider = services.BuildServiceProvider();
        ServiceContainer.Initialize(provider);

        var first  = ServiceContainer.GetService<ITestService>();
        var second = ServiceContainer.GetService<ITestService>();

        Assert.Same(first, second);
    }

    [Fact]
    public void GetService_Transient_DifferentInstancesReturned()
    {
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();
        var provider = services.BuildServiceProvider();
        ServiceContainer.Initialize(provider);

        var first  = ServiceContainer.GetService<ITestService>();
        var second = ServiceContainer.GetService<ITestService>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void GetService_UnregisteredType_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        ServiceContainer.Initialize(provider);

        Assert.Throws<InvalidOperationException>(() =>
            ServiceContainer.GetService<ITestService>());
    }

    [Fact]
    public void GetService_ByType_ReturnsInstance()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var provider = services.BuildServiceProvider();
        ServiceContainer.Initialize(provider);

        var result = ServiceContainer.GetService(typeof(ITestService));

        Assert.NotNull(result);
    }

    private interface ITestService { }
    private class TestService : ITestService { }
}
