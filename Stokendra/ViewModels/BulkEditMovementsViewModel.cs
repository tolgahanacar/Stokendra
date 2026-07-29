using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using Stokendra.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Stokendra.ViewModels;

public partial class BulkEditMovementsViewModel : ViewModelBase
{
    private readonly IMovementRepository _movements;
    private readonly IDepartmentRepository _departments;
    private readonly IDialogService _dialogService;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _errorMessage = "";

    // Bulk Apply Fields
    [ObservableProperty] private bool _isApplyDepartment;
    [ObservableProperty] private string _selectedDepartment = "";

    [ObservableProperty] private bool _isApplyRecipient;
    [ObservableProperty] private string _recipient = "";

    [ObservableProperty] private bool _isApplyDescription;
    [ObservableProperty] private string _description = "";

    [ObservableProperty] private bool _isApplyDate;
    [ObservableProperty] private DateTime? _date = DateTime.Today;

    partial void OnIsApplyDateChanged(bool value)
    {
        if (value && !Date.HasValue)
        {
            Date = EditingMovements.FirstOrDefault()?.Date.Date ?? DateTime.Today;
        }
    }

    public ObservableCollection<StockMovement> EditingMovements { get; } = new();
    public ObservableCollection<string> AllDepartments { get; } = new();

    public List<StockMovement>? Result { get; private set; }

    public BulkEditMovementsViewModel(
        List<StockMovement> selectedMovements,
        IMovementRepository movements,
        IDepartmentRepository departments,
        IDialogService dialogService)
    {
        _movements = movements;
        _departments = departments;
        _dialogService = dialogService;
        Title = LocalizationManager.L("bulk_edit_title", selectedMovements.Count);

        // Clone the selected movements so edits don't corrupt in-memory active list until saved
        foreach (var m in selectedMovements)
        {
            EditingMovements.Add(m.Clone());
        }

        Date = selectedMovements.FirstOrDefault()?.Date.Date ?? DateTime.Today;

        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        try
        {
            var depts = await _departments.GetAllAsync();
            foreach (var d in depts) AllDepartments.Add(d);
            
            SelectedDepartment = depts.FirstOrDefault() ?? "";
        }
        catch (Exception ex)
        {
            AppLogger.LogError("BulkEditMovementsViewModel init error", ex);
        }
    }

    [RelayCommand]
    private void ApplyBulk()
    {
        ErrorMessage = "";
        int updatedCount = 0;

        foreach (var m in EditingMovements)
        {
            if (IsApplyDepartment && !string.IsNullOrEmpty(SelectedDepartment))
            {
                m.Department = SelectedDepartment;
                updatedCount++;
            }
            if (IsApplyRecipient)
            {
                m.Recipient = Recipient ?? "";
                updatedCount++;
            }
            if (IsApplyDescription)
            {
                m.Description = Description ?? "";
                updatedCount++;
            }
            if (IsApplyDate && Date.HasValue)
            {
                // Preserve the original time part of the movement date, only update the date part
                m.Date = Date.Value.Date.Add(m.Date.TimeOfDay);
                updatedCount++;
            }
        }
        if (updatedCount > 0)
        {
            var temp = EditingMovements.ToList();
            EditingMovements.Clear();
            foreach (var m in temp) EditingMovements.Add(m);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = "";
        try
        {
            // Validate all inputs locally first
            foreach (var m in EditingMovements)
            {
                if (m.Quantity < 0)
                {
                    ErrorMessage = $"⚠️ {LocalizationManager.L("enter_valid_qty")} ({m.StockCardCode})";
                    return;
                }
                if (m.Quantity == 0 && m.TypeEnum != MovementType.Blank)
                {
                    ErrorMessage = $"⚠️ {LocalizationManager.L("enter_valid_qty")} ({m.StockCardCode})";
                    return;
                }
                if (string.IsNullOrWhiteSpace(m.Department))
                {
                    ErrorMessage = $"⚠️ {LocalizationManager.L("select_dept")} ({m.StockCardCode})";
                    return;
                }
            }

            // Perform transaction database save
            await _movements.UpdateBulkAsync(EditingMovements.ToList());

            Result = EditingMovements.ToList();
            CloseAction?.Invoke(true);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Bulk edit save error", ex);
            ErrorMessage = $"⚠️ {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel() => CloseAction?.Invoke(false);

    public Action<bool>? CloseAction { get; set; }
}
