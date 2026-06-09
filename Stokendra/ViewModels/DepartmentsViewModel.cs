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
    private readonly ILogger _logger;

    [ObservableProperty] private string _newDepartmentName = "";
    [ObservableProperty] private string? _selectedDepartment;
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<string> Departments { get; } = new();

    public DepartmentsViewModel(IDepartmentRepository departments, IDialogService dialogService, ILogger logger)
    {
        _departments = departments;
        _dialogService = dialogService;
        _logger = logger;
        _ = LoadAsync();
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
            _logger.LogError("Departments load error", ex);
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
            await _dialogService.ShowMessageAsync("Hata", $"Departman eklenemedi: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task Delete(string dept)
    {
        if (string.IsNullOrWhiteSpace(dept)) return;

        bool confirm = await _dialogService.ShowConfirmAsync("Onay", $"{dept} departmanını silmek istediğinize emin misiniz?");
        if (!confirm) return;

        try
        {
            await _departments.DeleteAsync(dept);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Hata", $"Departman silinemedi: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task Edit(string oldName)
    {
        if (string.IsNullOrWhiteSpace(oldName)) return;

        var vm = new PromptViewModel("Departman Düzenle", $"{oldName} departmanı için yeni isim giriniz:", oldName);
        if (await _dialogService.ShowDialogAsync(vm))
        {
            var newName = vm.InputText?.Trim();
            if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;

            try
            {
                await _departments.UpdateAsync(oldName, newName);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync("Hata", $"Güncelleme hatası: {ex.Message}");
            }
        }
    }
}
