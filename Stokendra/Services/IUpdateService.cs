using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.Services;

/// <summary>
/// Güncelleme bilgisi taşıyan DTO.
/// </summary>
public sealed class UpdateInfo
{
    /// <summary>Release tag adı (örn. "v7.4.0").</summary>
    public string TagName { get; init; } = "";

    /// <summary>Sürüm numarası (tag'den parse edilmiş).</summary>
    public Version Version { get; init; } = new(0, 0, 0);

    /// <summary>GitHub release sayfası URL'i.</summary>
    public string HtmlUrl { get; init; } = "";

    /// <summary>İndirilebilir installer asset URL'i (varsa).</summary>
    public string? InstallerDownloadUrl { get; init; }

    /// <summary>Installer dosya adı (varsa).</summary>
    public string? InstallerFileName { get; init; }

    /// <summary>Installer dosya boyutu (byte).</summary>
    public long InstallerSize { get; init; }

    /// <summary>Release notları (body).</summary>
    public string ReleaseNotes { get; init; } = "";

    /// <summary>Release yayın tarihi.</summary>
    public DateTime PublishedAt { get; init; }
}

public interface IUpdateService
{
    /// <summary>
    /// GitHub API'den en son release bilgisini çeker.
    /// </summary>
    Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// Installer dosyasını belirtilen hedefe indirir.
    /// </summary>
    /// <param name="downloadUrl">Dosya URL'i.</param>
    /// <param name="destinationPath">Hedef dosya yolu.</param>
    /// <param name="progress">İndirme yüzdesini raporlar (0-100).</param>
    /// <param name="ct">İptal token'ı.</param>
    Task DownloadInstallerAsync(string downloadUrl, string destinationPath, IProgress<int>? progress = null, CancellationToken ct = default);
}
