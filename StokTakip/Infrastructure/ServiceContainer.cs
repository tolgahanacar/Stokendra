using StokTakip.Data;
using StokTakip.Data.Interfaces;

namespace StokTakip.Infrastructure;

/// <summary>
/// Uygulama genelinde kullanılan basit, hafif bir IoC (Inversion of Control) container.
/// Microsoft.Extensions.DependencyInjection bağımlılığı olmadan singleton ve transient
/// kayıt/çözümleme desteği sağlar.
/// </summary>
public sealed class ServiceContainer : IDisposable
{
    private readonly Dictionary<Type, Func<object>> _factories = new();
    private readonly Dictionary<Type, object> _singletons = new();
    private bool _disposed;

    // ── Kayıt ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Bir türü singleton olarak kaydeder.
    /// İlk çözümlemede <paramref name="factory"/> çağrılır; sonraki çağrılarda aynı örnek döner.
    /// </summary>
    public ServiceContainer RegisterSingleton<TInterface, TImplementation>(Func<TImplementation> factory)
        where TImplementation : class, TInterface
    {
        _factories[typeof(TInterface)] = () =>
        {
            if (!_singletons.TryGetValue(typeof(TInterface), out var existing))
            {
                existing = factory();
                _singletons[typeof(TInterface)] = existing;
            }
            return existing;
        };
        return this;
    }

    /// <summary>
    /// Mevcut bir örneği singleton olarak kaydeder.
    /// </summary>
    public ServiceContainer RegisterInstance<TInterface>(TInterface instance)
        where TInterface : class
    {
        _singletons[typeof(TInterface)] = instance;
        _factories[typeof(TInterface)] = () => _singletons[typeof(TInterface)];
        return this;
    }

    /// <summary>
    /// Bir türü transient olarak kaydeder.
    /// Her çözümlemede <paramref name="factory"/> yeniden çağrılır.
    /// </summary>
    public ServiceContainer RegisterTransient<TInterface, TImplementation>(Func<TImplementation> factory)
        where TImplementation : class, TInterface
    {
        _factories[typeof(TInterface)] = () => factory();
        return this;
    }

    // ── Çözümleme ─────────────────────────────────────────────────────────

    /// <summary>
    /// Kayıtlı bir servisi çözümler.
    /// </summary>
    /// <typeparam name="T">İstenen servis türü.</typeparam>
    /// <returns>Servis örneği.</returns>
    /// <exception cref="InvalidOperationException">Tür kayıtlı değilse fırlatılır.</exception>
    public T Resolve<T>() where T : class
    {
        if (_factories.TryGetValue(typeof(T), out var factory))
            return (T)factory();

        throw new InvalidOperationException(
            $"'{typeof(T).Name}' türü ServiceContainer'a kayıtlı değil. " +
            $"Program.ConfigureServices() içinde RegisterSingleton veya RegisterInstance ile kaydedin.");
    }

    /// <summary>
    /// Kayıtlı bir servisi çözümlemeye çalışır. Kayıtlı değilse <c>null</c> döner.
    /// </summary>
    public T? TryResolve<T>() where T : class
    {
        return _factories.TryGetValue(typeof(T), out var factory) ? (T)factory() : null;
    }

    // ── IDisposable ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var singleton in _singletons.Values)
        {
            if (singleton is IDisposable disposable)
                disposable.Dispose();
        }

        _singletons.Clear();
        _factories.Clear();
    }
}
