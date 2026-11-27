using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data.Interfaces;
using Stokendra.Infrastructure;
using System.Security.Cryptography;
using System.Text;

namespace Stokendra.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IUserRepository _userRepository;

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DoLoginCommand))]
    private bool _isBusy = false;

    [ObservableProperty]
    private bool _isForgotMode = false;

    [ObservableProperty]
    private string _securityCode = "";

    [ObservableProperty]
    private string _newPassword = "";

    public string AppVersion => AppInfo.VersionString;

    public LoginViewModel(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task DoLoginAsync()
    {
        ErrorMessage = "";
        IsSuccess = false;
        IsBusy = true;

        try
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = LocalizationManager.L("username_password_required");
                return;
            }

            var (success, role) = await _userRepository.VerifyPasswordAsync(Username.Trim(), Password);

            if (success)
            {
                AppServices.Current.BeginSession(Username.Trim(), role);
                Password = "";
                LoginSuccessful?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorMessage = LocalizationManager.L("login_failed");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = string.Format(LocalizationManager.L("connection_error"), ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLogin() => !IsBusy;

    [RelayCommand]
    private void ToggleForgotMode()
    {
        IsForgotMode = !IsForgotMode;
        ErrorMessage = "";
        SecurityCode = "";
        NewPassword = "";
    }

    [RelayCommand]
    private async Task DoResetPasswordAsync()
    {
        ErrorMessage = "";
        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = LocalizationManager.L("enter_username");
            return;
        }

        // Gerçek bir ERP'de bu kod veritabanında saklanan bir hash veya 
        // e-posta/SMS ile gönderilen geçici bir kod olmalıdır.
        // Şimdilik güvenlik açığını gidermek için bu kontrolü devre dışı bırakıyoruz
        // veya ayarlardaki MasterCode ile eşleştiriyoruz.
        if (string.IsNullOrWhiteSpace(SecurityCode))
        {
            ErrorMessage = LocalizationManager.L("security_code_required");
            return;
        }

        // Master code kontrolü (zamanlama saldırılarına karşı sabit zamanlı karşılaştırma)
        string configuredMaster = (AppServices.Current.Settings.MasterSecurityCode ?? "").Trim();
        byte[] inputBytes = Encoding.UTF8.GetBytes(SecurityCode.Trim());
        byte[] masterBytes = Encoding.UTF8.GetBytes(configuredMaster);
        bool match = masterBytes.Length > 0 && inputBytes.Length == masterBytes.Length && CryptographicOperations.FixedTimeEquals(inputBytes, masterBytes);
        if (!match)
        {
            ErrorMessage = LocalizationManager.L("security_code_incorrect");
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 4)
        {
            ErrorMessage = LocalizationManager.L("password_min_length");
            return;
        }

        if (NewPassword.Contains(Username.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = LocalizationManager.L("password_contains_username");
            return;
        }

        var policyError = _userRepository.ValidatePasswordPolicy(NewPassword, Username.Trim());
        if (!string.IsNullOrEmpty(policyError))
        {
            ErrorMessage = policyError;
            return;
        }

        IsBusy = true;
        IsSuccess = false;
        try
        {
            bool ok = await _userRepository.ResetPasswordAsync(Username.Trim(), NewPassword);
            if (ok)
            {
                IsSuccess = true;
                ErrorMessage = LocalizationManager.L("password_reset_success");
                IsForgotMode = false;
                Password = "";
            }
            else
            {
                ErrorMessage = LocalizationManager.L("user_not_found");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"{LocalizationManager.L("error")}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Başarılı giriş sonrası App.axaml.cs tarafından dinlenir.</summary>
    public event EventHandler? LoginSuccessful;
}
