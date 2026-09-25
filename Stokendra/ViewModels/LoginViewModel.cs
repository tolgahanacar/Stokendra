using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data.Interfaces;
using Stokendra.Infrastructure;

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
    [NotifyCanExecuteChangedFor(nameof(DoLoginCommand))]
    private bool _isBusy = false;

    [ObservableProperty]
    private bool _isForgotMode = false;

    [ObservableProperty]
    private string _securityCode = "";

    [ObservableProperty]
    private string _newPassword = "";

    public string AppVersion => $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "7.3.9"}";

    public LoginViewModel(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task DoLoginAsync()
    {
        ErrorMessage = "";
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

        // Master code kontrolü (AppSettings'e eklenecek)
        if (SecurityCode != AppServices.Current.Settings.MasterSecurityCode)
        {
            ErrorMessage = LocalizationManager.L("security_code_incorrect");
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 4)
        {
            ErrorMessage = LocalizationManager.L("password_min_length");
            return;
        }

        // Şifre kullanıcı adını içermemeli
        if (NewPassword.Contains(Username.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = LocalizationManager.L("password_contains_username");
            return;
        }

        IsBusy = true;
        try
        {
            bool ok = await _userRepository.ResetPasswordAsync(Username.Trim(), NewPassword);
            if (ok)
            {
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
