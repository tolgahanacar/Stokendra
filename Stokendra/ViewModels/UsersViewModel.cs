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
    [ObservableProperty] private string _newUsername = "";
    [ObservableProperty] private string _newPassword = "";
    [ObservableProperty] private string _newRole = "user";
    [ObservableProperty] private User? _selectedUser;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isSuccess;
    [ObservableProperty] private bool _isLoading;
    private bool _isLoaded;

    public BulkObservableCollection<User> Users { get; } = new();
    public List<string> Roles { get; } = new() { "admin", "user" };

    public UsersViewModel(IUserRepository userRepository, IDialogService dialogService)
    {
        _userRepository = userRepository;
        _dialogService = dialogService;

        DataEvents.DataChanged += OnDataChanged;

        AsyncHelper.RunSafe(
            () => LoadUsersAsync(),
            ex => AppLogger.LogError("Users load failed", ex));
    }

    private void OnDataChanged(DataDomain domain)
    {
        if (domain == DataDomain.Users)
        {
            _isLoaded = false;
        }
    }

    public async Task EnsureLoadedAsync()
    {
        if (_isLoaded) return;
        await LoadUsersAsync();
    }

    [RelayCommand]
    public async Task LoadUsersAsync()
    {
        IsLoading = true;
        StatusMessage = "";
        try
        {
            var list = await _userRepository.GetUsersAsync();
            Users.ReplaceAll(list);
            _isLoaded = true;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Failed to load users", ex);
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
        if (!EnsureCanEditForSession()) return;
        StatusMessage = "";
        IsSuccess = false;

        string cleanUsername = (NewUsername ?? "").Trim();
        if (string.IsNullOrWhiteSpace(cleanUsername) || string.IsNullOrWhiteSpace(NewPassword))
        {
            StatusMessage = LocalizationManager.L("username_password_required");
            return;
        }

        var policyError = _userRepository.ValidatePasswordPolicy(NewPassword, cleanUsername);
        if (!string.IsNullOrEmpty(policyError))
        {
            StatusMessage = policyError;
            return;
        }

        IsLoading = true;
        try
        {
            bool ok = await _userRepository.AddUserAsync(cleanUsername, NewPassword, NewRole);
            if (ok)
            {
                StatusMessage = string.Format(LocalizationManager.L("user_added_success"), cleanUsername);
                IsSuccess = true;
                NewUsername = "";
                NewPassword = "";
                NewRole = "user";
                DataEvents.NotifyChanged(DataDomain.Users);
                await LoadUsersAsync();
            }
            else
            {
                StatusMessage = LocalizationManager.L("user_add_failed");
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Add user error", ex);
            StatusMessage = $"{LocalizationManager.L("error")}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task DeleteUserAsync(User? user)
    {
        if (!EnsureCanEditForSession()) return;
        var targetUser = user ?? SelectedUser;
        if (targetUser == null) return;
        if (targetUser.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), LocalizationManager.L("admin_user_cannot_delete"));
            return;
        }

        if (targetUser.Username.Equals(AppServices.Current.Session?.Username, StringComparison.OrdinalIgnoreCase))
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), LocalizationManager.L("cannot_delete_self"));
            return;
        }

        bool confirm = await _dialogService.ShowConfirmAsync(LocalizationManager.L("delete_user_title"), string.Format(LocalizationManager.L("confirm_delete_user"), targetUser.Username));
        if (!confirm) return;

        IsLoading = true;
        try
        {
            bool ok = await _userRepository.DeleteUserAsync(targetUser.Id);
            if (ok)
            {
                StatusMessage = LocalizationManager.L("user_deleted_success");
                IsSuccess = true;
                DataEvents.NotifyChanged(DataDomain.Users);
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
            AppLogger.LogError("Delete user error", ex);
            StatusMessage = $"{LocalizationManager.L("error")}: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ChangeRoleAsync(User? user)
    {
        if (!EnsureCanEditForSession()) return;
        var targetUser = user ?? SelectedUser;
        if (targetUser == null) return;
        if (targetUser.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), LocalizationManager.L("admin_role_cannot_change"));
            return;
        }

        if (targetUser.Username.Equals(AppServices.Current.Session?.Username, StringComparison.OrdinalIgnoreCase))
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), LocalizationManager.L("cannot_change_own_role"));
            return;
        }

        string nextRole = targetUser.Role == "admin" ? "user" : "admin";
        IsLoading = true;
        try
        {
            bool ok = await _userRepository.UpdateUserRoleAsync(targetUser.Id, nextRole);
            if (ok)
            {
                DataEvents.NotifyChanged(DataDomain.Users);
                await LoadUsersAsync();
                StatusMessage = string.Format(LocalizationManager.L("user_role_updated"), targetUser.Username, nextRole);
                IsSuccess = true;
            }
            else
            {
                StatusMessage = LocalizationManager.L("role_update_failed");
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Change user role error", ex);
            StatusMessage = $"{LocalizationManager.L("error")}: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ResetPasswordAsync(User? user)
    {
        if (!EnsureCanEditForSession()) return;
        var targetUser = user ?? SelectedUser;
        if (targetUser == null) return;

        var vm = new PromptViewModel(LocalizationManager.L("change_password"), $"{targetUser.Username} {LocalizationManager.L("new_password").ToTurkishLower()}:")
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

            var policyError = _userRepository.ValidatePasswordPolicy(newPassword, targetUser.Username);
            if (!string.IsNullOrEmpty(policyError))
            {
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), policyError);
                return;
            }

            IsLoading = true;
            try
            {
                bool ok = await _userRepository.ResetPasswordAsync(targetUser.Username, newPassword);
                if (ok)
                {
                    await _dialogService.ShowMessageAsync(LocalizationManager.L("info"), LocalizationManager.L("password_changed"));
                    StatusMessage = $"{targetUser.Username} - {LocalizationManager.L("password_changed")}";
                    IsSuccess = true;
                }
                else
                {
                    await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), LocalizationManager.L("password_change_failed"));
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Reset password error", ex);
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
        NewUsername = "";
        NewPassword = "";
        StatusMessage = "";
        _isLoaded = false;

        if (IsAdmin)
        {
            AsyncHelper.RunSafe(
                () => LoadUsersAsync(),
                ex => AppLogger.LogError("Users refresh failed", ex));
        }
        else
        {
            Users.Clear();
            SelectedUser = null;
        }
    }
}
