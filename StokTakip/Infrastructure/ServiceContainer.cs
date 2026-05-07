using System.Reflection;

namespace StokTakip.Infrastructure;

/// <summary>
/// Uygulama genelinde kullanılan hafif IoC (Inversion of Control) container.
/// Constructor injection desteği sağlar; böylece sınıflar bağımlılıklarını
/// açıkça constructor üzerinden talep edebilir.
/// </summary>
public sealed class ServiceContainer : IDisposable
{
    private readonly Dictionary<Type, Func<object>> _factories = new();
    private readonly Dictionary<Type, object> _singletons = new();
    private bool _disposed;

    // ── Kayıt ─────────────────────────────────────────────────────────────

    public ServiceContainer RegisterSingleton<TInterface, TImplementation>()
        where TImplementation : class, TInterface
    {
        _factories[typeof(TInterface)] = () =>
        {
            lock (_singletons)
            {
                if (!_singletons.TryGetValue(typeof(TInterface), out var existing))
                {
                    existing = CreateInstance(typeof(TImplementation));
                    _singletons[typeof(TInterface)] = existing;
                }
                return existing;
            }
        };
        return this;
    }

    public ServiceContainer RegisterSingleton<TInterface>(Func<object> factory)
    {
        _factories[typeof(TInterface)] = () =>
        {
            lock (_singletons)
            {
                if (!_singletons.TryGetValue(typeof(TInterface), out var existing))
                {
                    existing = factory();
                    _singletons[typeof(TInterface)] = existing;
                }
                return existing;
            }
        };
        return this;
    }

    public ServiceContainer RegisterInstance<TInterface>(TInterface instance)
        where TInterface : class
    {
        _singletons[typeof(TInterface)] = instance;
        _factories[typeof(TInterface)] = () => _singletons[typeof(TInterface)];
        return this;
    }

    public ServiceContainer RegisterTransient<TInterface, TImplementation>()
        where TImplementation : class, TInterface
    {
        _factories[typeof(TInterface)] = () => CreateInstance(typeof(TImplementation));
        return this;
    }

    // ── Çözümleme ─────────────────────────────────────────────────────────

    public T Resolve<T>() where T : class => (T)Resolve(typeof(T));

    public object Resolve(Type type)
    {
        if (_factories.TryGetValue(type, out var factory))
            return factory();

        // Eğer tip kayıtlı değilse ama concrete bir class ise, 
        // otomatik olarak instantiate etmeyi dene (Formlar için faydalı)
        if (!type.IsAbstract && !type.IsInterface)
            return CreateInstance(type);

        throw new InvalidOperationException($"Type '{type.Name}' is not registered.");
    }

    private object CreateInstance(Type type)
    {
        var constructors = type.GetConstructors();
        if (constructors.Length == 0)
            return Activator.CreateInstance(type)!;

        // En çok parametre alan constructor'ı seç (basit bir strateji)
        var constructor = constructors.OrderByDescending(c => c.GetParameters().Length).First();
        var parameters = constructor.GetParameters();
        var parameterInstances = new object[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            parameterInstances[i] = Resolve(parameters[i].ParameterType);
        }

        return constructor.Invoke(parameterInstances);
    }

    // ── IDisposable ────────────────────────────────────────────────────────

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

