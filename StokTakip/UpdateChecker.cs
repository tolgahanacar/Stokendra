using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using static StokTakip.LocalizationManager;

namespace StokTakip;

public static class UpdateChecker
{
    public static async Task CheckManualAsync()
    {
        Cursor.Current = Cursors.WaitCursor;
        try
        {
            if (!await DoCheckAsync(true))
            {
                MessageBox.Show(L("up_to_date", "v" + Application.ProductVersion), L("info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex) { MessageBox.Show(L("update_error", ex.Message), L("error"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { Cursor.Current = Cursors.Default; }
    }

    public static async Task CheckSilentAsync()
    {
        try
        {
            await DoCheckAsync(false);
        }
        catch { } // Ignore errors in silent check
    }

    private static async Task<bool> DoCheckAsync(bool isManual)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Stokendra-App");
        string url = "https://api.github.com/repos/tolgahanacar/Stokendra/releases/latest";
        var response = await client.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var jsonString = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(jsonString);
            string latestVersion = jsonDoc.RootElement.GetProperty("tag_name").GetString() ?? "";
            string htmlUrl = jsonDoc.RootElement.GetProperty("html_url").GetString() ?? "";
            string currentVersion = "v" + Application.ProductVersion;
            
            Version vLatest = ParseVersion(latestVersion);
            Version vCurrent = ParseVersion(currentVersion);

            if (vLatest > vCurrent)
            {
                if (MessageBox.Show(L("update_available", latestVersion, currentVersion), L("update_title"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                { 
                    Process.Start(new ProcessStartInfo { FileName = htmlUrl, UseShellExecute = true }); 
                }
                return true;
            }
        }
        return false;
    }

    private static Version ParseVersion(string v)
    {
        v = v.ToLower().Replace("v", "").Trim();
        if (Version.TryParse(v, out var parsed)) return parsed;
        return new Version(0,0,0,0);
    }
}
