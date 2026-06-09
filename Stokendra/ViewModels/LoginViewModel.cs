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
    private bool _isBusy = false;

    [ObservableProperty]
    private bool _isForgotMode = false;

    [ObservableProperty]
    private string _securityCode = "";

    [ObservableProperty]
    private string _newPassword = "";

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
                ErrorMessage = "Kullanıcı adı ve şifre boş olamaz.";
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
                ErrorMessage = "Kullanıcı adı veya şifre hatalı.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Bağlantı hatası: {ex.Message}";
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
            ErrorMessage = "Lütfen kullanıcı adınızı girin.";
            return;
        }

        // Gerçek bir ERP'de bu kod veritabanında saklanan bir hash veya 
        // e-posta/SMS ile gönderilen geçici bir kod olmalıdır.
        // Şimdilik güvenlik açığını gidermek için bu kontrolü devre dışı bırakıyoruz
        // veya ayarlardaki MasterCode ile eşleştiriyoruz.
        if (string.IsNullOrWhiteSpace(SecurityCode))
        {
            ErrorMessage = "Güvenlik kodu gereklidir.";
            return;
        }

        // Master code kontrolü (AppSettings'e eklenecek)
        if (SecurityCode != AppServices.Current.Settings.MasterSecurityCode)
        {
            ErrorMessage = "Güvenlik kodu hatalı.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 4)
        {
            ErrorMessage = "Yeni şifre en az 4 karakter olmalıdır.";
            return;
        }

        // Şifre kullanıcı adını içermemeli
        if (NewPassword.Contains(Username.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Şifre kullanıcı adını içeremez.";
            return;
        }

        IsBusy = true;
        try
        {
            bool ok = await _userRepository.ResetPasswordAsync(Username.Trim(), NewPassword);
            if (ok)
            {
                ErrorMessage = "Şifre başarıyla sıfırlandı. Giriş yapabilirsiniz.";
                IsForgotMode = false;
                Password = "";
            }
            else
            {
                ErrorMessage = "Kullanıcı bulunamadı.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Hata: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Başarılı giriş sonrası App.axaml.cs tarafından dinlenir.</summary>
    public event EventHandler? LoginSuccessful;
}
