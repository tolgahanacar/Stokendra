using Xunit;
using NSubstitute;
using Stokendra.ViewModels;
using Stokendra.Data.Interfaces;
using Stokendra.Infrastructure;
using System.Threading.Tasks;

namespace Stokendra.Tests;

public class ServicesViewModelTests
{
    private readonly IServiceRecordRepository _servicesMock;
    private readonly IDialogService _dialogMock;

    public ServicesViewModelTests()
    {
        LocalizationManager.Initialize("tr");
        _servicesMock = Substitute.For<IServiceRecordRepository>();
        _dialogMock = Substitute.For<IDialogService>();
    }

    private ServicesViewModel CreateViewModel()
    {
        return new ServicesViewModel(_servicesMock, _dialogMock);
    }

    [Fact]
    public async Task Test_ExportExcelCommand_InvokesSaveFileDialog()
    {
        // Arrange
        var vm = CreateViewModel();
        _dialogMock.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<string?>("C:\\Temp\\services.xlsx"));

        // Act
        await vm.ExportExcelCommand.ExecuteAsync(null);

        // Assert
        await _dialogMock.Received(1).SaveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Test_ImportCommand_InvokesOpenFileDialog()
    {
        // Arrange
        var vm = CreateViewModel();
        _dialogMock.OpenFileAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<string?>("C:\\Temp\\services_to_import.xlsx"));

        // Act
        await vm.ImportCommand.ExecuteAsync(null);

        // Assert
        await _dialogMock.Received(1).OpenFileAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Test_DeleteCommand_AsksForConfirmation()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.SelectedRecord = new Stokendra.Models.ServiceRecord { Id = 1, DeviceName = "Laptop" };
        
        _dialogMock.ShowConfirmAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(false)); // Cancel delete

        // Act
        await vm.DeleteCommand.ExecuteAsync(null);

        // Assert
        await _dialogMock.Received(1).ShowConfirmAsync(Arg.Any<string>(), Arg.Any<string>());
        await _servicesMock.DidNotReceive().DeleteAsync(Arg.Any<int>());
    }
}
