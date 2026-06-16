using System;
using System.IO;

namespace Stokendra.Tests;

public class TestSandbox : IDisposable
{
    private readonly string _testSandboxDir;

    public TestSandbox()
    {
        // Create a unique temporary directory for this test class instance
        _testSandboxDir = Path.Combine(Path.GetTempPath(), $"Stokendra_Test_Sandbox_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testSandboxDir);

        // Redirect the application paths to the clean sandbox folder
        AppPaths.TestDataRootOverride = _testSandboxDir;
    }

    public void Dispose()
    {
        // Revert the path override
        AppPaths.TestDataRootOverride = null;

        // Delete the temporary directory and all files inside it
        try
        {
            if (Directory.Exists(_testSandboxDir))
            {
                Directory.Delete(_testSandboxDir, true);
            }
        }
        catch { }
    }
}
