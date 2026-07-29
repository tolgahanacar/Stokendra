using Xunit;
using NSubstitute;
using Stokendra.Data;
using Stokendra.ViewModels;
using Stokendra.Data.Interfaces;
using Stokendra.Services;
using Stokendra.Infrastructure;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Data;

namespace Stokendra.Tests;

public class DialogShownException : Exception
{
    public string Title { get; }
    public DialogShownException(string title, string message) : base(message)
    {
        Title = title;
    }
}

public class SettingsViewModelTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly Database _database;
    private readonly AppServices _appServices;
    private readonly TestSandbox _sandbox;

    private readonly IUserRepository _usersMock;
    private readonly IConfigRepository _configMock;
    private readonly IDialogService _dialogServiceMock;
    private readonly IBackupService _backupServiceMock;
    private readonly IDbConnectionFactory _connectionFactoryMock;
    private readonly IUpdateService _updateServiceMock;

    public SettingsViewModelTests()
    {
        LocalizationManager.Initialize("tr");
        _sandbox = new TestSandbox();
        
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"Stokendra_Test_Settings_{Guid.NewGuid():N}.db");
        _database = new Database(_tempDbPath);

        var appSettings = new AppSettings
        {
            DbPath = _tempDbPath,
            CompanyName = "Initial Company",
            Language = "tr",
            AutoBackupPath = Path.Combine(Path.GetTempPath(), "InitialBackup"),
            LowStockThreshold = 3,
            MasterSecurityCode = "1234"
        };
        _appServices = new AppServices(appSettings, _database);

        _usersMock = Substitute.For<IUserRepository>();
        _configMock = Substitute.For<IConfigRepository>();
        _dialogServiceMock = Substitute.For<IDialogService>();
        _backupServiceMock = Substitute.For<IBackupService>();
        _connectionFactoryMock = Substitute.For<IDbConnectionFactory>();
        _updateServiceMock = Substitute.For<IUpdateService>();
    }

    public void Dispose()
    {
        _appServices.Dispose();
        _database.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
        _sandbox.Dispose();
    }

    private SettingsViewModel CreateViewModel(IDbConnectionFactory? connFactory = null)
    {
        return new SettingsViewModel(
            _usersMock,
            _configMock,
            _dialogServiceMock,
            _backupServiceMock,
            connFactory ?? new SqliteConnectionFactory(_tempDbPath),
            _updateServiceMock
        );
    }

    [Fact]
    public async Task Test_SaveSettings_SavesCorrectly_WhenNoLanguageOrDbPathChange()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.CompanyName = "Updated Company Ltd";
        vm.AutoBackupPath = "C:\\NewBackup";
        vm.LowStockThreshold = 8;
        vm.MasterSecurityCode = "8888";
        vm.SelectedLanguage = "Türkçe"; // Match initial "tr" -> no change

        // Act
        await vm.SaveSettingsAsync();

        // Assert
        Assert.Equal(LocalizationManager.L("settings_saved"), vm.StatusMessage);
        Assert.True(vm.IsSuccess);
        
        // Verify values saved to AppServices Settings
        var settings = AppServices.Current.Settings;
        Assert.Equal("Updated Company Ltd", settings.CompanyName);
        Assert.Equal("C:\\NewBackup", settings.AutoBackupPath);
        Assert.Equal(8, settings.LowStockThreshold);
        Assert.Equal("8888", settings.MasterSecurityCode);

        // Verify values pushed to Database Config Repo
        _configMock.Received(1).SetConfig("CompanyName", "Updated Company Ltd");
        _configMock.Received(1).SetConfig("AutoBackupPath", "C:\\NewBackup");
        _configMock.Received(1).SetConfig("LowStockThreshold", "8");
        _configMock.Received(1).SetConfig("MasterSecurityCode", "8888");
        _configMock.Received(1).WriteAuditLog("Update", "AppConfig", 0, Arg.Any<string>());
    }

    [Fact]
    public async Task Test_SaveSettings_LanguageChange_TriggersRestartFlow()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.SelectedLanguage = "English"; // Init was Türkçe -> changes to en

        // Intercept the restart dialog show to prevent calling Environment.Exit(0)
        _dialogServiceMock.When(x => x.ShowMessageAsync(Arg.Any<string>(), Arg.Any<string>()))
            .Do(x => throw new DialogShownException(x.ArgAt<string>(0), x.ArgAt<string>(1)));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DialogShownException>(() => vm.SaveSettingsAsync());
        
        Assert.Equal(LocalizationManager.L("info"), ex.Title);
        Assert.Equal(LocalizationManager.L("restart_required"), ex.Message);
        Assert.Equal("en", AppServices.Current.Settings.Language);
    }

    [Fact]
    public async Task Test_SaveSettings_DbPathChange_TriggersRestartFlow()
    {
        // Arrange
        var vm = CreateViewModel();
        string newDbPath = Path.Combine(Path.GetTempPath(), "AnotherDatabase.db");
        vm.DbPath = newDbPath; // Changes path

        // Intercept dialog show to prevent exit
        _dialogServiceMock.When(x => x.ShowMessageAsync(Arg.Any<string>(), Arg.Any<string>()))
            .Do(x => throw new DialogShownException(x.ArgAt<string>(0), x.ArgAt<string>(1)));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DialogShownException>(() => vm.SaveSettingsAsync());

        Assert.Equal(LocalizationManager.L("info"), ex.Title);
        Assert.Equal(LocalizationManager.L("db_path_changed_restart"), ex.Message);
        Assert.Equal(newDbPath, AppServices.Current.Settings.DbPath);
    }

    [Fact]
    public async Task Test_SaveSettings_DbWriteError_DoesNotCrash()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.CompanyName = "Error Corp";

        // Throws DB exception during SetConfig
        _configMock.When(x => x.SetConfig(Arg.Any<string>(), Arg.Any<string>()))
            .Do(x => throw new Exception("Simulated SQLite write failure"));

        // Act
        await vm.SaveSettingsAsync();

        // Assert
        // Should catch the write error, log it, and complete without throwing
        Assert.Equal(LocalizationManager.L("settings_saved"), vm.StatusMessage);
        Assert.True(vm.IsSuccess);
    }

    [Fact]
    public async Task Test_SelectBackupFolder_UpdatesProperty()
    {
        // Arrange
        var vm = CreateViewModel();
        _dialogServiceMock.OpenFolderAsync(Arg.Any<string>())
            .Returns(Task.FromResult<string?>("D:\\AutoBackupDirectory"));

        // Act
        await vm.SelectBackupFolder();

        // Assert
        Assert.Equal("D:\\AutoBackupDirectory", vm.AutoBackupPath);
    }

    [Fact]
    public async Task Test_SelectDbFile_UpdatesProperty()
    {
        // Arrange
        var vm = CreateViewModel();
        _dialogServiceMock.OpenFileAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<string?>("D:\\new_database.db"));

        // Act
        await vm.SelectDbFile();

        // Assert
        Assert.Equal("D:\\new_database.db", vm.DbPath);
    }

    [Fact]
    public async Task Test_ChangePassword_EmptyFields_Fails()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.OldPassword = "";
        vm.NewPassword = "new";
        vm.ConfirmPassword = "";

        // Act
        await vm.ChangePasswordAsync();

        // Assert
        Assert.Equal(LocalizationManager.L("fill_all_password_fields"), vm.StatusMessage);
        Assert.False(vm.IsSuccess);
    }

    [Fact]
    public async Task Test_ChangePassword_Mismatch_Fails()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.OldPassword = "old";
        vm.NewPassword = "new1";
        vm.ConfirmPassword = "new2";

        // Act
        await vm.ChangePasswordAsync();

        // Assert
        Assert.Equal(LocalizationManager.L("passwords_dont_match"), vm.StatusMessage);
        Assert.False(vm.IsSuccess);
    }

    [Fact]
    public async Task Test_ChangePassword_PolicyViolation_Fails()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.OldPassword = "old";
        vm.NewPassword = "abc"; // too short (length < 4)
        vm.ConfirmPassword = "abc";

        _usersMock.ValidatePasswordPolicy("abc", Arg.Any<string>())
            .Returns(LocalizationManager.L("password_min_length"));

        // Act
        await vm.ChangePasswordAsync();

        // Assert
        Assert.Equal(LocalizationManager.L("password_min_length"), vm.StatusMessage);
        Assert.False(vm.IsSuccess);
    }

    [Fact]
    public async Task Test_ChangePassword_IncorrectOldPassword_Fails()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.OldPassword = "wrong_old_password";
        vm.NewPassword = "new_strong_password";
        vm.ConfirmPassword = "new_strong_password";

        _usersMock.ValidatePasswordPolicy(Arg.Any<string>(), Arg.Any<string>())
            .Returns((string?)null); // Policy OK

        _usersMock.ChangePasswordAsync(Arg.Any<string>(), "wrong_old_password", "new_strong_password")
            .Returns(Task.FromResult(false)); // Repo fails change because old pass incorrect

        // Act
        await vm.ChangePasswordAsync();

        // Assert
        Assert.Equal(LocalizationManager.L("current_password_incorrect"), vm.StatusMessage);
        Assert.False(vm.IsSuccess);
    }

    [Fact]
    public async Task Test_ChangePassword_Succeeds()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.OldPassword = "correct_old";
        vm.NewPassword = "correct_new";
        vm.ConfirmPassword = "correct_new";

        _usersMock.ValidatePasswordPolicy(Arg.Any<string>(), Arg.Any<string>())
            .Returns((string?)null); // Policy OK

        _usersMock.ChangePasswordAsync(Arg.Any<string>(), "correct_old", "correct_new")
            .Returns(Task.FromResult(true)); // Repo changes successfully

        // Act
        await vm.ChangePasswordAsync();

        // Assert
        Assert.Equal(LocalizationManager.L("password_changed_success"), vm.StatusMessage);
        Assert.True(vm.IsSuccess);
        Assert.Empty(vm.OldPassword);
        Assert.Empty(vm.NewPassword);
        Assert.Empty(vm.ConfirmPassword);
    }

    [Fact]
    public async Task Test_DatabaseBackup_Cancels()
    {
        // Arrange
        var vm = CreateViewModel();
        _dialogServiceMock.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<string?>(null)); // User cancelled

        // Act
        await vm.DatabaseBackupAsync();

        // Assert
        _configMock.DidNotReceiveWithAnyArgs().CreateBackup(null!);
    }

    [Fact]
    public async Task Test_DatabaseBackup_Succeeds()
    {
        // Arrange
        var vm = CreateViewModel();
        string destPath = "C:\\Backups\\backup.db";
        _dialogServiceMock.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<string?>(destPath));

        // Act
        await vm.DatabaseBackupAsync();

        // Assert
        _configMock.Received(1).CreateBackup(destPath);
        Assert.Equal(LocalizationManager.L("db_backup_success"), vm.StatusMessage);
        Assert.True(vm.IsSuccess);
    }

    [Fact]
    public async Task Test_DatabaseBackup_ThrowsException_DoesNotCrash()
    {
        // Arrange
        var vm = CreateViewModel();
        string destPath = "C:\\Backups\\backup.db";
        _dialogServiceMock.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<string?>(destPath));

        // Throw file IO exception during backup
        _configMock.When(x => x.CreateBackup(destPath))
            .Do(x => throw new IOException("Disk Full"));

        // Act
        await vm.DatabaseBackupAsync();

        // Assert
        Assert.False(vm.IsSuccess);
        Assert.Contains("Disk Full", vm.StatusMessage);
    }

    [Fact]
    public async Task Test_FullBackup_Success()
    {
        // Arrange
        var vm = CreateViewModel();
        string destZip = Path.Combine(Path.GetTempPath(), $"full_{Guid.NewGuid():N}.zip");
        _dialogServiceMock.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<string?>(destZip));

        string tempZip = Path.Combine(Path.GetTempPath(), $"excel_{Guid.NewGuid():N}.zip");
        File.WriteAllText(tempZip, "dummy zip content");

        _backupServiceMock.ExportAllExcelAsync(Arg.Any<string>())
            .Returns(Task.FromResult(tempZip));

        // Act
        await vm.FullBackupAsync();

        // Assert
        Assert.Equal(LocalizationManager.L("full_backup_success"), vm.StatusMessage);
        Assert.True(vm.IsSuccess);
        
        // Clean up dummy zip if test runner created it
        if (File.Exists(destZip)) File.Delete(destZip);
    }

    [Fact]
    public async Task Test_FullBackup_ThrowsException_DoesNotCrash()
    {
        // Arrange
        var vm = CreateViewModel();
        string destZip = "C:\\backups\\full.zip";
        _dialogServiceMock.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<string?>(destZip));

        _backupServiceMock.When(x => x.ExportAllExcelAsync(Arg.Any<string>()))
            .Do(x => throw new UnauthorizedAccessException("Access denied to Temp folder"));

        // Act
        await vm.FullBackupAsync();

        // Assert
        Assert.False(vm.IsSuccess);
        Assert.Contains("Access denied to Temp folder", vm.StatusMessage);
    }

    [Fact]
    public async Task Test_TruncateAuditLog_Cancel()
    {
        // Arrange
        var vm = CreateViewModel();
        _dialogServiceMock.ShowConfirmAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(false)); // Cancel

        // Act
        await vm.TruncateAuditLog();

        // Assert
        _configMock.DidNotReceive().TruncateAuditLog();
    }

    [Fact]
    public async Task Test_TruncateAuditLog_Success()
    {
        // Arrange
        var vm = CreateViewModel();
        _dialogServiceMock.ShowConfirmAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true)); // Confirmed

        // Act
        await vm.TruncateAuditLog();

        // Assert
        _configMock.Received(1).TruncateAuditLog();
        Assert.Equal(LocalizationManager.L("audit_log_cleared_success"), vm.StatusMessage);
        Assert.True(vm.IsSuccess);
    }

    [Fact]
    public async Task Test_TruncateAuditLog_ThrowsException_DoesNotCrash()
    {
        // Arrange
        var vm = CreateViewModel();
        _dialogServiceMock.ShowConfirmAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));

        _configMock.When(x => x.TruncateAuditLog())
            .Do(x => throw new Exception("Database locked"));

        // Act
        await vm.TruncateAuditLog();

        // Assert
        Assert.False(vm.IsSuccess);
        Assert.Contains("Database locked", vm.StatusMessage);
    }

    [Fact]
    public async Task Test_CheckDatabaseIntegrity_Success()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.CheckDatabaseIntegrityAsync();

        // Assert
        Assert.Equal(LocalizationManager.L("db_integrity_success_status"), vm.StatusMessage);
        Assert.True(vm.IsSuccess);
    }

    [Fact]
    public async Task Test_CheckDatabaseIntegrity_ThrowsException_DoesNotCrash()
    {
        // Arrange
        _connectionFactoryMock.When(x => x.CreateConnection())
            .Do(x => throw new InvalidOperationException("Failed to open connection"));
        var vm = CreateViewModel(_connectionFactoryMock);

        // Act
        await vm.CheckDatabaseIntegrityAsync();

        // Assert
        Assert.False(vm.IsSuccess);
        Assert.Contains("Failed to open connection", vm.StatusMessage);
    }

    [Fact]
    public async Task Test_OptimizeDatabase_Success()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.OptimizeDatabaseAsync();

        // Assert
        Assert.Equal(LocalizationManager.L("db_optimize_success_status"), vm.StatusMessage);
        Assert.True(vm.IsSuccess);
    }

    [Fact]
    public async Task Test_OptimizeDatabase_ThrowsException_DoesNotCrash()
    {
        // Arrange
        _connectionFactoryMock.When(x => x.CreateConnection())
            .Do(x => throw new Exception("Disk write failure"));
        var vm = CreateViewModel(_connectionFactoryMock);

        // Act
        await vm.OptimizeDatabaseAsync();

        // Assert
        Assert.False(vm.IsSuccess);
        Assert.Contains("Disk write failure", vm.StatusMessage);
    }


    [Fact]
    public async Task Test_CheckForUpdates_NewVersionAvailable_PromptsUser()
    {
        // Arrange
        var vm = CreateViewModel();
        _updateServiceMock.GetLatestReleaseAsync()
            .Returns(Task.FromResult(("v9.9.9", "https://github.com/tolgahanacar/Stokendra/releases/tag/v9.9.9")));
        _dialogServiceMock.ShowConfirmAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));

        // Act
        await vm.CheckForUpdatesAsync();

        // Assert
        Assert.True(vm.IsSuccess);
        Assert.Contains("v9.9.9", vm.StatusMessage);
        await _dialogServiceMock.Received(1).ShowConfirmAsync(
            LocalizationManager.L("update_title"), 
            Arg.Any<string>()
        );
    }

    [Fact]
    public async Task Test_CheckForUpdates_AppUpToDate()
    {
        // Arrange
        var vm = CreateViewModel();
        // Current version is retrieved from Assembly, usually v1.0.0 or v5.0.0 in release, but in tests it's Assembly version (e.g. 1.0.0.0 or similar)
        // CleanLatest parses clean version. Since current version of test assembly defaults to 1.0.0 (or what is set in AssemblyInfo),
        // we can set latest release to "v1.0.0" to verify up-to-date message.
        // Let's set latest to the same as vm.AppVersion
        string currentVer = vm.AppVersion.TrimStart('v', 'V');
        _updateServiceMock.GetLatestReleaseAsync()
            .Returns(Task.FromResult(($"v{currentVer}", "https://github.com/releases/")));

        // Act
        await vm.CheckForUpdatesAsync();

        // Assert
        Assert.True(vm.IsSuccess);
        Assert.Contains(currentVer, vm.StatusMessage);
        await _dialogServiceMock.Received(1).ShowMessageAsync(
            LocalizationManager.L("info"), 
            string.Format(LocalizationManager.L("up_to_date"), vm.AppVersion)
        );
    }

    [Fact]
    public async Task Test_CheckForUpdates_Exception_DoesNotCrash()
    {
        // Arrange
        _updateServiceMock.When(x => x.GetLatestReleaseAsync())
            .Do(x => throw new System.Net.Http.HttpRequestException("No internet connection"));
        var vm = CreateViewModel();

        // Act
        await vm.CheckForUpdatesAsync();

        // Assert
        Assert.False(vm.IsSuccess);
        Assert.Contains("No internet connection", vm.StatusMessage);
        await _dialogServiceMock.Received(1).ShowMessageAsync(
            LocalizationManager.L("error"), 
            Arg.Any<string>()
        );
    }
}
