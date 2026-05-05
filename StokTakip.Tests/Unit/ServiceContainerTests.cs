using StokTakip.Infrastructure;

namespace StokTakip.Tests.Unit;

/// <summary>
/// <see cref="ServiceContainer"/> için unit testler.
/// Singleton, transient ve instance kayıt/çözümleme davranışlarını doğrular.
/// </summary>
public class ServiceContainerTests
{
    // ── Singleton ─────────────────────────────────────────────────────────

    [Fact]
    public void RegisterSingleton_SameInstanceReturnedOnMultipleCalls()
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>(() => new TestService());

        var first = container.Resolve<ITestService>();
        var second = container.Resolve<ITestService>();

        Assert.Same(first, second);
    }

    [Fact]
    public void RegisterInstance_ReturnsRegisteredInstance()
    {
        using var container = new ServiceContainer();
        var instance = new TestService();
        container.RegisterInstance<ITestService>(instance);

        var resolved = container.Resolve<ITestService>();

        Assert.Same(instance, resolved);
    }

    // ── Transient ─────────────────────────────────────────────────────────

    [Fact]
    public void RegisterTransient_DifferentInstancesReturnedOnMultipleCalls()
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>(() => new TestService());

        var first = container.Resolve<ITestService>();
        var second = container.Resolve<ITestService>();

        Assert.NotSame(first, second);
    }

    // ── Hata durumları ────────────────────────────────────────────────────

    [Fact]
    public void Resolve_UnregisteredType_ThrowsInvalidOperationException()
    {
        using var container = new ServiceContainer();

        var ex = Assert.Throws<InvalidOperationException>(() => container.Resolve<ITestService>());
        Assert.Contains("ITestService", ex.Message);
    }

    [Fact]
    public void TryResolve_UnregisteredType_ReturnsNull()
    {
        using var container = new ServiceContainer();

        var result = container.TryResolve<ITestService>();

        Assert.Null(result);
    }

    [Fact]
    public void TryResolve_RegisteredType_ReturnsInstance()
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>(() => new TestService());

        var result = container.TryResolve<ITestService>();

        Assert.NotNull(result);
    }

    // ── Dispose ───────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CallsDisposeOnSingletonInstances()
    {
        var container = new ServiceContainer();
        var disposable = new DisposableTestService();
        container.RegisterInstance<ITestService>(disposable);

        // Singleton'ı çözümle (oluşturulsun)
        _ = container.Resolve<ITestService>();
        container.Dispose();

        Assert.True(disposable.IsDisposed);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes_NoException()
    {
        var container = new ServiceContainer();
        container.Dispose();
        container.Dispose(); // İkinci çağrı exception fırlatmamalı
    }

    // ── Test yardımcıları ─────────────────────────────────────────────────

    private interface ITestService { }

    private class TestService : ITestService { }

    private class DisposableTestService : ITestService, IDisposable
    {
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }
}
