using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.Services;

/// <summary>
/// GitHub Releases API üzerinden güncelleme kontrolü ve installer indirme servisi.
/// </summary>
public class UpdateService : IUpdateService
{
    private static readonly HttpClient _client = new();
    private const string ApiUrl = "https://api.github.com/repos/tolgahanacar/Stokendra/releases/latest";

    static UpdateService()
    {
        _client.DefaultRequestHeaders.Add("User-Agent", "Stokendra-Updater");
        _client.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var response = await _client.GetAsync(ApiUrl, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string tagName = root.GetProperty("tag_name").GetString() ?? "v0.0.0";
        string htmlUrl = root.GetProperty("html_url").GetString() ?? "";
        string body = root.TryGetProperty("body", out var bodyProp) ? (bodyProp.GetString() ?? "") : "";
        DateTime publishedAt = root.TryGetProperty("published_at", out var pubProp) 
            ? (DateTime.TryParse(pubProp.GetString(), out var dt) ? dt : DateTime.MinValue) 
            : DateTime.MinValue;

        string cleanTag = tagName.TrimStart('v', 'V');
        if (!Version.TryParse(cleanTag, out var version))
            return null;

        // Asset'lerden installer (.exe) dosyasını bul
        string? installerUrl = null;
        string? installerFileName = null;
        long installerSize = 0;

        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                string? name = asset.GetProperty("name").GetString();
                if (name != null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) 
                    && name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                {
                    installerUrl = asset.GetProperty("browser_download_url").GetString();
                    installerFileName = name;
                    installerSize = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
                    break;
                }
            }
        }

        return new UpdateInfo
        {
            TagName = tagName,
            Version = version,
            HtmlUrl = htmlUrl,
            InstallerDownloadUrl = installerUrl,
            InstallerFileName = installerFileName,
            InstallerSize = installerSize,
            ReleaseNotes = body,
            PublishedAt = publishedAt
        };
    }

    public async Task DownloadInstallerAsync(string downloadUrl, string destinationPath, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        using var response = await _client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;

        string? directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Atomik yazma: önce .tmp'ye yaz, sonra taşı
        string tempPath = destinationPath + ".tmp";

        try
        {
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int lastReportedPercent = -1;

            while (true)
            {
                int bytesRead = await contentStream.ReadAsync(buffer, ct);
                if (bytesRead == 0) break;

                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;

                if (totalBytes > 0 && progress != null)
                {
                    int percent = (int)(totalRead * 100 / totalBytes);
                    if (percent != lastReportedPercent)
                    {
                        lastReportedPercent = percent;
                        progress.Report(percent);
                    }
                }
            }
        }
        catch
        {
            // Başarısız indirmede temp dosyayı temizle
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            throw;
        }

        // Başarılı indirme: temp → asıl dosya
        if (File.Exists(destinationPath))
            File.Delete(destinationPath);
        File.Move(tempPath, destinationPath);
    }
}
