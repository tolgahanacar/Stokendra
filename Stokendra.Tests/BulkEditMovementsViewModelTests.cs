using Xunit;
using NSubstitute;
using Stokendra.ViewModels;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using Stokendra.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stokendra.Tests;

public class BulkEditMovementsViewModelTests
{
    private readonly IMovementRepository _movementsMock;
    private readonly IDepartmentRepository _departmentsMock;
    private readonly IDialogService _dialogMock;

    public BulkEditMovementsViewModelTests()
    {
        LocalizationManager.Initialize("tr");
        _movementsMock = Substitute.For<IMovementRepository>();
        _departmentsMock = Substitute.For<IDepartmentRepository>();
        _dialogMock = Substitute.For<IDialogService>();
    }

    [Fact]
    public void Test_StockMovement_Department_ProtectsAgainstNullOrWhitespaceOverwrite()
    {
        var movement = new StockMovement
        {
            Id = 1,
            Department = "Üretim"
        };

        // Attempt to overwrite with null (simulating Avalonia ComboBox detachment)
        movement.Department = null!;
        Assert.Equal("Üretim", movement.Department);

        // Attempt to overwrite with empty string
        movement.Department = "";
        Assert.Equal("Üretim", movement.Department);

        // Attempt to overwrite with whitespace
        movement.Department = "   ";
        Assert.Equal("Üretim", movement.Department);

        // Valid change should succeed
        movement.Department = "Pazarlama";
        Assert.Equal("Pazarlama", movement.Department);
    }

    [Fact]
    public async Task Test_ApplyBulk_OnlyDescription_PreservesDepartments()
    {
        // Arrange
        var depts = new List<string> { "Üretim", "Genel", "Depo" };
        _departmentsMock.GetAllAsync().Returns(Task.FromResult(depts));

        var movements = new List<StockMovement>
        {
            new StockMovement { Id = 1, StockCardCode = "001", Quantity = 5, Department = "Üretim", Description = "Eski 1" },
            new StockMovement { Id = 2, StockCardCode = "002", Quantity = 10, Department = "Genel", Description = "Eski 2" },
            new StockMovement { Id = 3, StockCardCode = "003", Quantity = 15, Department = "Depo", Description = "Eski 3" }
        };

        var vm = new BulkEditMovementsViewModel(movements, _movementsMock, _departmentsMock, _dialogMock);
        await Task.Delay(50); // wait for InitAsync

        // Act: Apply ONLY description
        vm.IsApplyDescription = true;
        vm.Description = "Yeni Açıklama";
        vm.IsApplyDepartment = false; // Department checkbox is NOT checked

        vm.ApplyBulkCommand.Execute(null);

        // Assert
        Assert.Equal("Yeni Açıklama", vm.EditingMovements[0].Description);
        Assert.Equal("Yeni Açıklama", vm.EditingMovements[1].Description);
        Assert.Equal("Yeni Açıklama", vm.EditingMovements[2].Description);

        // Departments must NOT be cleared or changed!
        Assert.Equal("Üretim", vm.EditingMovements[0].Department);
        Assert.Equal("Genel", vm.EditingMovements[1].Department);
        Assert.Equal("Depo", vm.EditingMovements[2].Department);
    }

    [Fact]
    public async Task Test_ApplyBulk_EmptyDescription_PreservesDepartments()
    {
        // User's exact scenario: Check description checkbox, leave it empty (or clear it)
        var depts = new List<string> { "Üretim", "Genel" };
        _departmentsMock.GetAllAsync().Returns(Task.FromResult(depts));

        var movements = new List<StockMovement>
        {
            new StockMovement { Id = 1, StockCardCode = "001", Quantity = 5, Department = "Üretim", Description = "Eski 1" },
            new StockMovement { Id = 2, StockCardCode = "002", Quantity = 10, Department = "Genel", Description = "Eski 2" }
        };

        var vm = new BulkEditMovementsViewModel(movements, _movementsMock, _departmentsMock, _dialogMock);
        await Task.Delay(50);

        // Check description, set empty string
        vm.IsApplyDescription = true;
        vm.Description = "";
        vm.IsApplyDepartment = false;

        vm.ApplyBulkCommand.Execute(null);

        // All descriptions cleared as requested
        Assert.Equal("", vm.EditingMovements[0].Description);
        Assert.Equal("", vm.EditingMovements[1].Description);

        // Departments intact
        Assert.Equal("Üretim", vm.EditingMovements[0].Department);
        Assert.Equal("Genel", vm.EditingMovements[1].Department);
    }

    [Fact]
    public async Task Test_SaveAsync_AutoAppliesCheckedBulkFieldsAndSaves()
    {
        var depts = new List<string> { "Üretim" };
        _departmentsMock.GetAllAsync().Returns(Task.FromResult(depts));

        var movements = new List<StockMovement>
        {
            new StockMovement { Id = 1, StockCardCode = "001", Quantity = 5, Department = "Üretim", Description = "Eski" }
        };

        var vm = new BulkEditMovementsViewModel(movements, _movementsMock, _departmentsMock, _dialogMock);
        await Task.Delay(50);

        bool? closed = null;
        vm.CloseAction = result => closed = result;

        // User checks bulk description, enters text, but DOES NOT manually click "Uygula"
        vm.IsApplyDescription = true;
        vm.Description = "Otomatik Uygulanan Açıklama";

        // Clicks Save directly
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.True(closed);
        Assert.NotNull(vm.Result);
        Assert.Equal("Otomatik Uygulanan Açıklama", vm.Result[0].Description);
        Assert.Equal("Üretim", vm.Result[0].Department);
        await _movementsMock.Received(1).UpdateBulkAsync(Arg.Any<List<StockMovement>>());
    }

    [Fact]
    public async Task Test_SaveAsync_Fails_WhenValidationFails()
    {
        var depts = new List<string> { "Üretim" };
        _departmentsMock.GetAllAsync().Returns(Task.FromResult(depts));

        var movements = new List<StockMovement>
        {
            new StockMovement { Id = 1, StockCardCode = "001", Quantity = -5, Department = "Üretim" }
        };

        var vm = new BulkEditMovementsViewModel(movements, _movementsMock, _departmentsMock, _dialogMock);
        await Task.Delay(50);

        bool? closed = null;
        vm.CloseAction = result => closed = result;

        await vm.SaveCommand.ExecuteAsync(null);

        // Should NOT close, should set error message
        Assert.Null(closed);
        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
        await _movementsMock.DidNotReceive().UpdateBulkAsync(Arg.Any<List<StockMovement>>());
    }
}
