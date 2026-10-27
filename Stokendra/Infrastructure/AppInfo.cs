using System;
using System.Reflection;

namespace Stokendra;

/// <summary>
/// Merkezi uygulama bilgileri ve sürüm yönetimi.
/// Tüm ViewModel'lar ve servisler sürüm bilgisini buradan tek kaynak olarak alır.
/// </summary>
public static class AppInfo
{
    public const string Name = "Stokendra";

    private static readonly Version _version = 
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(7, 4, 1);

    /// <summary>Uygulama sürümü nesnesi.</summary>
    public static Version Version => _version;

    /// <summary>Arayüzde gösterilecek sürüm etiketi (örn: "v7.4.0").</summary>
    public static string VersionString => $"v{_version.ToString(3)}";

    /// <summary>Formatlanmamış sürüm metni (örn: "7.4.0").</summary>
    public static string RawVersion => _version.ToString(3);
}
