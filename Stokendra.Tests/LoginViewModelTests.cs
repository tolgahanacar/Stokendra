using Xunit;
using NSubstitute;
using Stokendra.Data;
using Stokendra.ViewModels;
using Stokendra.Data.Interfaces;
using Stokendra.Infrastructure;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Stokendra.Tests;

public class TestViewModel : ViewModelBase
{
    // A concrete implementation of ViewModelBase to check CanEdit/IsAdmin behaviour
}

public class LoginViewModelTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly Database _database;
    private readonly AppServices _appServices;
    private readonly TestSandbox _sandbox;

    private readonly IUserRepository _usersMock;

    public LoginViewModelTests()
    {
        LocalizationManager.Initialize("tr");
        _sandbox = new TestSandbox();
        
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"Stokendra_Test_Login_{Guid.NewGuid():N}.db");
        _database = new Database(_tempDbPath);

        var appSettings = new AppSettings
        {
            DbPath = _tempDbPath,
            CompanyName = "Test Company",
            Language = "tr",
            MasterSecurityCode = "7777"
        };
        _appServices = new AppServices(appSettings, _database);

        _usersMock = Substitute.For<IUserRepository>();
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

    private LoginViewModel CreateViewModel()
    {
        return new LoginViewModel(_usersMock);
    }

    [Fact]
    public async Task Test_DoLogin_EmptyUsernameOrPassword_Fails()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Username = "   ";
        vm.Password = "12345";

        // Act
        vm.DoLoginCommand.Execute(null);

        // Assert
        Assert.Equal(LocalizationManager.L("username_password_required"), vm.ErrorMessage);
        Assert.Null(AppServices.Current.Session);
    }

    [Fact]
    public async Task Test_DoLogin_WrongPassword_Fails()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Username = "admin";
        vm.Password = "wrong_password";

        _usersMock.VerifyPasswordAsync("admin", "wrong_password")
            .Returns(Task.FromResult((false, ""))); // login fails

        // Act
        // CommunityToolkit.Mvvm command Execute on async can be waited by calling it
        // Or executing directly to invoke DoLoginAsync
        // Since command is DoLoginCommand, we can execute the task:
        await vm.DoLoginCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(LocalizationManager.L("login_failed"), vm.ErrorMessage);
        Assert.Null(AppServices.Current.Session);
    }

    [Fact]
    public async Task Test_DoLogin_ConnectionError_DoesNotCrash()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Username = "admin";
        vm.Password = "12345";

        _usersMock.When(x => x.VerifyPasswordAsync("admin", "12345"))
            .Do(x => throw new InvalidOperationException("SQLite connection timeout"));

        // Act
        await vm.DoLoginCommand.ExecuteAsync(null);

        // Assert
        // Should catch exception gracefully and show error message
        Assert.Contains("SQLite connection timeout", vm.ErrorMessage);
        Assert.Null(AppServices.Current.Session);
    }

    [Fact]
    public async Task Test_DoLogin_Success_InitiatesSession()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Username = "admin";
        vm.Password = "correct_password";

        _usersMock.VerifyPasswordAsync("admin", "correct_password")
            .Returns(Task.FromResult((true, "admin")));

        bool loginEventTriggered = false;
        vm.LoginSuccessful += (sender, args) => loginEventTriggered = true;

        // Act
        await vm.DoLoginCommand.ExecuteAsync(null);

        // Assert
        Assert.True(loginEventTriggered);
        Assert.NotNull(AppServices.Current.Session);
        Assert.Equal("admin", AppServices.Current.Session.Username);
        Assert.Equal("admin", AppServices.Current.Session.Role);
        Assert.Empty(vm.ErrorMessage);
    }

    [Fact]
    public async Task Test_DoResetPassword_EmptyUsername_Fails()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Username = "";
        vm.SecurityCode = "7777";
        vm.NewPassword = "newpassword";

        // Act
        await vm.DoResetPasswordCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(LocalizationManager.L("enter_username"), vm.ErrorMessage);
    }

    [Fact]
    public async Task Test_DoResetPassword_EmptySecurityCode_Fails()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Username = "admin";
        vm.SecurityCode = "";
        vm.NewPassword = "newpassword";

        // Act
        await vm.DoResetPasswordCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(LocalizationManager.L("security_code_required"), vm.ErrorMessage);
    }

    [Fact]
    public async Task Test_DoResetPassword_WrongSecurityCode_Fails()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Username = "admin";
        vm.SecurityCode = "wrong_security_code";
        vm.NewPassword = "newpassword";

        // Act
        await vm.DoResetPasswordCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(LocalizationManager.L("security_code_incorrect"), vm.ErrorMessage);
    }

    [Fact]
    public async Task Test_DoResetPassword_ShortNewPassword_Fails()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Username = "admin";
        vm.SecurityCode = "7777"; // Correct master code
        vm.NewPassword = "123"; // Too short (min 4 required)

        // Act
        await vm.DoResetPasswordCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(LocalizationManager.L("password_min_length"), vm.ErrorMessage);
    }

    [Fact]
    public async Task Test_DoResetPassword_NewPasswordContainsUsername_Fails()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Username = "myuser";
        vm.SecurityCode = "7777";
        vm.NewPassword = "supermyuserpass"; // contains user

        // Act
        await vm.DoResetPasswordCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(LocalizationManager.L("password_contains_username"), vm.ErrorMessage);
    }

    [Fact]
    public async Task Test_DoResetPassword_Success()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Username = "user1";
        vm.SecurityCode = "7777";
        vm.NewPassword = "safe_new_password";

        _usersMock.ResetPasswordAsync("user1", "safe_new_password")
            .Returns(Task.FromResult(true));

        // Act
        await vm.DoResetPasswordCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(LocalizationManager.L("password_reset_success"), vm.ErrorMessage);
        Assert.False(vm.IsForgotMode);
        Assert.Empty(vm.Password);
    }

    [Fact]
    public async Task Test_DoResetPassword_Exception_DoesNotCrash()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.Username = "user1";
        vm.SecurityCode = "7777";
        vm.NewPassword = "safe_new_password";

        _usersMock.When(x => x.ResetPasswordAsync("user1", "safe_new_password"))
            .Do(x => throw new Exception("Database disk read error"));

        // Act
        await vm.DoResetPasswordCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("Database disk read error", vm.ErrorMessage);
    }

    [Fact]
    public void Test_RoleAuthorization_Admin_CanEditAndIsAdmin()
    {
        // Arrange
        AppServices.Current.BeginSession("administrator", "admin");

        // Act
        var testVm = new TestViewModel();

        // Assert
        Assert.True(testVm.CanEdit);
        Assert.True(testVm.IsAdmin);
    }

    [Fact]
    public void Test_RoleAuthorization_User_CannotEditAndIsNotAdmin()
    {
        // Arrange
        AppServices.Current.BeginSession("staff_member", "user");

        // Act
        var testVm = new TestViewModel();

        // Assert
        Assert.False(testVm.CanEdit);
        Assert.False(testVm.IsAdmin);
    }

    [Fact]
    public void Test_RoleAuthorization_RefreshSession_UpdatesPrivileges()
    {
        // Arrange
        AppServices.Current.BeginSession("staff_member", "user");
        var testVm = new TestViewModel();
        Assert.False(testVm.CanEdit);

        // Act - Log in as admin and refresh
        AppServices.Current.BeginSession("admin_user", "admin");
        testVm.RefreshSession();

        // Assert
        Assert.True(testVm.CanEdit);
    }
}
