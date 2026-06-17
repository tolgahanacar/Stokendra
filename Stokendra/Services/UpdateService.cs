using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Stokendra.Services;

public class UpdateService : IUpdateService
{
    private static readonly HttpClient _client = new();

    static UpdateService()
    {
        _client.DefaultRequestHeaders.Add("User-Agent", "Stokendra-Updater");
    }

    public async Task<(string TagName, string HtmlUrl)> GetLatestReleaseAsync()
    {
        var response = await _client.GetAsync("https://api.github.com/repos/tolgahanacar/Stokendra/releases/latest");
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"GitHub API returned status code {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string tagName = root.GetProperty("tag_name").GetString() ?? "v1.0.0";
        string htmlUrl = root.GetProperty("html_url").GetString() ?? "https://github.com/tolgahanacar/Stokendra/releases/latest";

        return (tagName, htmlUrl);
    }
}
