using Xunit;
using Stokendra;

namespace Stokendra.Tests;

public class AppInfoTests
{
    [Fact]
    public void Test_AppInfo_ReturnsValidVersion()
    {
        Assert.NotNull(AppInfo.Version);
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.VersionString));
        Assert.StartsWith("v", AppInfo.VersionString);
        Assert.Equal("Stokendra", AppInfo.Name);
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.RawVersion));
    }
}
