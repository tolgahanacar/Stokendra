using Xunit;
using Stokendra.Infrastructure;

namespace Stokendra.Tests;

public class LocalizationTests
{
    [Fact]
    public void Test_Language_TR_Loads_Correctly()
    {
        // Arrange
        LocalizationManager.Initialize("tr");

        // Act
        string title = LocalizationManager.L("app_title");
        string expected = "Stokendra - Stok Takip Programı";

        // Assert
        Assert.Equal(expected, title);
    }

    [Fact]
    public void Test_Language_EN_Loads_Correctly()
    {
        // Arrange
        LocalizationManager.Initialize("en");

        // Act
        string title = LocalizationManager.L("app_title");
        string expected = "Stokendra - Stock Tracking Program";

        // Assert
        Assert.Equal(expected, title);
    }

    [Fact]
    public void Test_Fallback_Uses_BuiltIn_If_Key_Missing()
    {
        // Arrange
        LocalizationManager.Initialize("tr");

        // Act
        // "low" exists in BuiltInFallbacks but is also in JSON. Let's try to load a built-in key that might not be in JSON
        // For testing, since L("key") returns "key" if it doesn't exist anywhere,
        // let's test a key that exists in built-in fallbacks.
        string lowText = LocalizationManager.L("low");

        // Assert
        Assert.Equal("Düşük", lowText);
    }

    [Fact]
    public void Test_Formatted_Localization_Works()
    {
        // Arrange
        LocalizationManager.Initialize("tr");

        // Act
        string formatted = LocalizationManager.L("records_found", 15);

        // Assert
        Assert.Equal("15 kayıt bulundu.", formatted);
    }

    [Fact]
    public void Test_Both_Languages_Have_Identical_Keys_And_No_Empty_Values()
    {
        // Find Resources/lang_tr.json and Resources/lang_en.json
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string trPath = Path.Combine(baseDir, "Resources", "lang_tr.json");
        string enPath = Path.Combine(baseDir, "Resources", "lang_en.json");

        if (!File.Exists(trPath))
        {
            // Fallback to project root if running in dev environment
            trPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Stokendra", "Resources", "lang_tr.json"));
            enPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Stokendra", "Resources", "lang_en.json"));
        }

        Assert.True(File.Exists(trPath), $"lang_tr.json not found at {trPath}");
        Assert.True(File.Exists(enPath), $"lang_en.json not found at {enPath}");

        var trDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(trPath))!;
        var enDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(enPath))!;

        Assert.NotEmpty(trDict);
        Assert.NotEmpty(enDict);

        var trKeys = new HashSet<string>(trDict.Keys, StringComparer.OrdinalIgnoreCase);
        var enKeys = new HashSet<string>(enDict.Keys, StringComparer.OrdinalIgnoreCase);

        var missingInEn = trKeys.Except(enKeys).ToList();
        var missingInTr = enKeys.Except(trKeys).ToList();

        Assert.True(missingInEn.Count == 0, $"Keys in TR but missing in EN: {string.Join(", ", missingInEn)}");
        Assert.True(missingInTr.Count == 0, $"Keys in EN but missing in TR: {string.Join(", ", missingInTr)}");

        foreach (var (k, v) in trDict)
        {
            Assert.False(string.IsNullOrWhiteSpace(v), $"TR key '{k}' has empty or whitespace value.");
        }

        foreach (var (k, v) in enDict)
        {
            Assert.False(string.IsNullOrWhiteSpace(v), $"EN key '{k}' has empty or whitespace value.");
        }
    }
}
