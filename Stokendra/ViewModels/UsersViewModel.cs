using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data.Interfaces;
using Stokendra.Infrastructure;
using Stokendra.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.ViewModels;

public partial class UsersViewModel : ViewModelBase
{
    private readonly IUserRepository _userRepository;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;

    [ObservableProperty] private string _newUsername = "";
    [ObservableProperty] private string _newPassword = "";
    [ObservableProperty] private string _newRole = "user";
    [ObservableProperty] private User? _selectedUser;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isSuccess;
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<User> Users { get; } = new();
    public List<string> Roles { get; } = new() { "admin", "user" };

    public UsersViewModel(IUserRepository userRepository, IDialogService dialogService, ILogger logger)
    {
        _userRepository = userRepository;
        _dialogService = dialogService;
        _logger = logger;
        _ = LoadUsersAsync();
    }

    [RelayCommand]
    public async Task LoadUsersAsync()
    {
        IsLoading = true;
        StatusMessage = "";
        try
        {
            Users.Clear();
            var list = await _userRepository.GetUsersAsync();
            foreach (var user in list)
            {
                Users.Add(user);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load users", ex);
            StatusMessage = $"Hata: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task AddUserAsync()
    {
        StatusMessage = "";
        IsSuccess = false;

        if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewPassword))
        {
            StatusMessage = "Kullanıcı adı ve şifre boş olamaz.";
            return;
        }

        var policyError = _userRepository.ValidatePasswordPolicy(NewPassword, NewUsername);
        if (policyError != null)
        {
            StatusMessage = policyError;
            return;
        }

        IsLoading = true;
        try
        {
            bool ok = await _userRepository.AddUserAsync(NewUsername.Trim(), NewPassword, NewRole);
            if (ok)
            {
                StatusMessage = $"{NewUsername} kullanıcısı başarıyla eklendi.";
                IsSuccess = true;
                NewUsername = "";
                NewPassword = "";
                NewRole = "user";
                await LoadUsersAsync();
            }
            else
            {
                StatusMessage = "Kullanıcı eklenemedi. Bu isimde bir kullanıcı zaten var olabilir.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Add user error", ex);
            StatusMessage = $"Hata: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task DeleteUserAsync(User user)
    {
        if (user == null) return;
        if (user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            await _dialogService.ShowMessageAsync("Hata", "Ana 'admin' kullanıcısı silinemez.");
            return;
        }

        if (user.Username.Equals(AppServices.Current.Session?.Username, StringComparison.OrdinalIgnoreCase))
        {
            await _dialogService.ShowMessageAsync("Hata", "Kendinizi silemezsiniz.");
            return;
        }

        bool confirm = await _dialogService.ShowConfirmAsync("Kullanıcı Sil", $"'{user.Username}' kullanıcısı silinecektir. Emin misiniz?");
        if (!confirm) return;

        IsLoading = true;
        try
        {
            bool ok = await _userRepository.DeleteUserAsync(user.Id);
            if (ok)
            {
                StatusMessage = "Kullanıcı silindi.";
                IsSuccess = true;
                await LoadUsersAsync();
            }
            else
            {
                StatusMessage = "Kullanıcı silinemedi.";
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Delete user error", ex);
            StatusMessage = $"Hata: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ChangeRoleAsync(User user)
    {
        if (user == null) return;
        if (user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            await _dialogService.ShowMessageAsync("Hata", "Ana 'admin' kullanıcısının rolü değiştirilemez.");
            return;
        }

        if (user.Username.Equals(AppServices.Current.Session?.Username, StringComparison.OrdinalIgnoreCase))
        {
            await _dialogService.ShowMessageAsync("Hata", "Kendi rolünüzü değiştiremezsiniz.");
            return;
        }

        string nextRole = user.Role == "admin" ? "user" : "admin";
        bool ok = await _userRepository.UpdateUserRoleAsync(user.Id, nextRole);
        if (ok)
        {
            await LoadUsersAsync();
            StatusMessage = $"{user.Username} kullanıcısının rolü '{nextRole}' olarak güncellendi.";
            IsSuccess = true;
        }
        else
        {
            StatusMessage = "Rol güncellenemedi.";
            IsSuccess = false;
        }
    }

    public override void RefreshSession()
    {
        base.RefreshSession();
        _ = LoadUsersAsync();
    }
}
