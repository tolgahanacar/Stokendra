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
            StatusMessage = $"{LocalizationManager.L("error")}: {ex.Message}";
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
            StatusMessage = LocalizationManager.L("username_password_required");
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
                StatusMessage = string.Format(LocalizationManager.L("user_added_success"), NewUsername);
                IsSuccess = true;
                NewUsername = "";
                NewPassword = "";
                NewRole = "user";
                await LoadUsersAsync();
            }
            else
            {
                StatusMessage = LocalizationManager.L("user_add_failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Add user error", ex);
            StatusMessage = $"{LocalizationManager.L("error")}: {ex.Message}";
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
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), LocalizationManager.L("admin_user_cannot_delete"));
            return;
        }

        if (user.Username.Equals(AppServices.Current.Session?.Username, StringComparison.OrdinalIgnoreCase))
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), LocalizationManager.L("cannot_delete_self"));
            return;
        }

        bool confirm = await _dialogService.ShowConfirmAsync(LocalizationManager.L("delete_user_title"), string.Format(LocalizationManager.L("confirm_delete_user"), user.Username));
        if (!confirm) return;

        IsLoading = true;
        try
        {
            bool ok = await _userRepository.DeleteUserAsync(user.Id);
            if (ok)
            {
                StatusMessage = LocalizationManager.L("user_deleted_success");
                IsSuccess = true;
                await LoadUsersAsync();
            }
            else
            {
                StatusMessage = LocalizationManager.L("user_delete_failed");
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Delete user error", ex);
            StatusMessage = $"{LocalizationManager.L("error")}: {ex.Message}";
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
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), LocalizationManager.L("admin_role_cannot_change"));
            return;
        }

        if (user.Username.Equals(AppServices.Current.Session?.Username, StringComparison.OrdinalIgnoreCase))
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), LocalizationManager.L("cannot_change_own_role"));
            return;
        }

        string nextRole = user.Role == "admin" ? "user" : "admin";
        bool ok = await _userRepository.UpdateUserRoleAsync(user.Id, nextRole);
        if (ok)
        {
            await LoadUsersAsync();
            StatusMessage = string.Format(LocalizationManager.L("user_role_updated"), user.Username, nextRole);
            IsSuccess = true;
        }
        else
        {
            StatusMessage = LocalizationManager.L("role_update_failed");
            IsSuccess = false;
        }
    }

    [RelayCommand]
    public async Task ResetPasswordAsync(User user)
    {
        if (user == null) return;

        var vm = new PromptViewModel(LocalizationManager.L("change_password"), $"{user.Username} {LocalizationManager.L("new_password").ToTurkishLower()}:")
        {
            PasswordChar = '●',
            Watermark = LocalizationManager.L("new_password")
        };

        if (await _dialogService.ShowDialogAsync(vm))
        {
            var newPassword = vm.InputText?.Trim();
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), LocalizationManager.L("password_empty"));
                return;
            }

            var policyError = _userRepository.ValidatePasswordPolicy(newPassword, user.Username);
            if (policyError != null)
            {
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), policyError);
                return;
            }

            IsLoading = true;
            try
            {
                bool ok = await _userRepository.ResetPasswordAsync(user.Username, newPassword);
                if (ok)
                {
                    await _dialogService.ShowMessageAsync(LocalizationManager.L("info"), LocalizationManager.L("password_changed"));
                    StatusMessage = $"{user.Username} - {LocalizationManager.L("password_changed")}";
                    IsSuccess = true;
                }
                else
                {
                    await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), LocalizationManager.L("password_change_failed"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Reset password error", ex);
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), string.Format(LocalizationManager.L("password_change_error"), ex.Message));
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public override void RefreshSession()
    {
        base.RefreshSession();
        _ = LoadUsersAsync();
    }
}
