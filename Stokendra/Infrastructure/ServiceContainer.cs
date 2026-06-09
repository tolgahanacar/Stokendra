using System;
using Microsoft.Extensions.DependencyInjection;

namespace Stokendra.Infrastructure;

/// <summary>
/// IServiceProvider için statik bir erişim noktası sağlar.
/// Uygulama genelinde servislerin çözümlenmesini kolaylaştırır.
/// </summary>
public static class ServiceContainer
{
    private static IServiceProvider? _provider;

    public static void Initialize(IServiceProvider provider)
    {
        _provider = provider;
    }

    public static T GetService<T>() where T : notnull
    {
        if (_provider == null)
            throw new InvalidOperationException("ServiceContainer is not initialized.");

        return _provider.GetRequiredService<T>();
    }

    public static object GetService(Type type)
    {
        if (_provider == null)
            throw new InvalidOperationException("ServiceContainer is not initialized.");

        return _provider.GetRequiredService(type);
    }
}
