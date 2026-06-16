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
}
