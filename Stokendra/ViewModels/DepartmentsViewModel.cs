using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data.Interfaces;
using Stokendra.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Stokendra.ViewModels;

public partial class DepartmentsViewModel : ViewModelBase
{
    private readonly IDepartmentRepository _departments;
    private readonly IDialogService _dialogService;
    [ObservableProperty] private string _newDepartmentName = "";
    [ObservableProperty] private string? _selectedDepartment;
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<string> Departments { get; } = new();

    public DepartmentsViewModel(IDepartmentRepository departments, IDialogService dialogService)
    {
        _departments = departments;
        _dialogService = dialogService;
        AsyncHelper.RunSafe(
            () => LoadAsync(),
            ex => AppLogger.LogError("Departments load failed", ex));
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var list = await _departments.GetAllAsync();
            Departments.Clear();
            foreach (var d in list) Departments.Add(d);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Departments load error", ex);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task Add()
    {
        if (string.IsNullOrWhiteSpace(NewDepartmentName)) return;

        try
        {
            await _departments.AddAsync(NewDepartmentName);
            await LoadAsync();
            NewDepartmentName = "";
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), string.Format(LocalizationManager.L("dept_added_failed"), ex.Message));
        }
    }

    [RelayCommand]
    public async Task Delete(string? dept)
    {
        string targetDept = string.IsNullOrWhiteSpace(dept) ? SelectedDepartment ?? "" : dept;
        if (string.IsNullOrWhiteSpace(targetDept)) return;

        bool confirm = await _dialogService.ShowConfirmAsync(LocalizationManager.L("dept_delete_confirm_title"), string.Format(LocalizationManager.L("dept_delete_confirm_msg"), targetDept));
        if (!confirm) return;

        try
        {
            await _departments.DeleteAsync(targetDept);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), string.Format(LocalizationManager.L("dept_deleted_failed"), ex.Message));
        }
    }

    [RelayCommand]
    public async Task Edit(string? oldName)
    {
        string targetDept = string.IsNullOrWhiteSpace(oldName) ? SelectedDepartment ?? "" : oldName;
        if (string.IsNullOrWhiteSpace(targetDept)) return;

        var vm = new PromptViewModel(LocalizationManager.L("edit_dept_title"), string.Format(LocalizationManager.L("edit_dept_prompt"), targetDept), targetDept);
        if (await _dialogService.ShowDialogAsync(vm))
        {
            var newName = vm.InputText?.Trim();
            if (string.IsNullOrWhiteSpace(newName) || newName == targetDept) return;

            try
            {
                await _departments.UpdateAsync(targetDept, newName);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), string.Format(LocalizationManager.L("dept_update_failed"), ex.Message));
            }
        }
    }
}
