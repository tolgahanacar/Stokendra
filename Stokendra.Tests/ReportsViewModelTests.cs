using Xunit;
using NSubstitute;
using Stokendra.ViewModels;
using Stokendra.Data.Interfaces;
using Stokendra.Infrastructure;
using System;
using System.Threading.Tasks;

namespace Stokendra.Tests;

public class ReportsViewModelTests
{
    private readonly IMovementRepository _movementsMock;
    private readonly IDepartmentRepository _departmentsMock;
    private readonly IStockCardRepository _cardsMock;
    private readonly IDialogService _dialogMock;

    public ReportsViewModelTests()
    {
        LocalizationManager.Initialize("tr");
        var tempDb = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Stokendra_Test_{Guid.NewGuid():N}.db");
        var settings = new AppSettings { Language = "tr" };
        var db = new Stokendra.Data.Database(tempDb);
        _ = new AppServices(settings, db); // Initialize AppServices.Current
        
        _movementsMock = Substitute.For<IMovementRepository>();
        _departmentsMock = Substitute.For<IDepartmentRepository>();
        _cardsMock = Substitute.For<IStockCardRepository>();
        _dialogMock = Substitute.For<IDialogService>();
    }

    private ReportsViewModel CreateViewModel()
    {
        return new ReportsViewModel(_movementsMock, _departmentsMock, _cardsMock, _dialogMock);
    }

    [Fact]
    public void Test_ClearFiltersCommand_ResetsAllFilters()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.StartDate = new DateTime(2023, 1, 1);
        vm.EndDate = new DateTime(2023, 12, 31);
        vm.SelectedDepartment = "IT";
        vm.SelectedCategory = "Electronics";

        // Act
        vm.ClearFiltersCommand.Execute(null);

        // Assert
        Assert.Equal(LocalizationManager.L("all"), vm.SelectedDepartment);
        Assert.Equal(LocalizationManager.L("all"), vm.SelectedCategory);
        Assert.Equal(LocalizationManager.L("all"), vm.SelectedUser);
        Assert.Equal(DateTime.Today.AddDays(-30), vm.StartDate);
        Assert.Equal(DateTime.Today, vm.EndDate.Date);
    }

    [Fact]
    public async Task Test_ExportCommand_InvokesSaveFileDialog()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.ReportRows = new System.Collections.Generic.List<Stokendra.Models.StockMovement> { new Stokendra.Models.StockMovement() };
        _dialogMock.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<string?>("C:\\Temp\\report.xlsx"));

        // Act
        await vm.ExportCommand.ExecuteAsync(null);

        // Assert
        await _dialogMock.Received(1).SaveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Test_PrintCommand_CreatesHtml_And_ShowsPreview()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.ReportRows = new System.Collections.Generic.List<Stokendra.Models.StockMovement> { new Stokendra.Models.StockMovement() };

        // Act
        await vm.PrintCommand.ExecuteAsync(null);

        // Assert
        // Ensures that the print preview dialog is invoked
        await _dialogMock.Received(1).ShowDialogAsync(Arg.Any<PrintPreviewViewModel>());
    }
}
