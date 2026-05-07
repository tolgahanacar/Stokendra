using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Infrastructure;

namespace StokTakip.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IUserRepository _userRepository;

    [ObservableProperty]
    private string _username = "admin";   // Geliştirme kolaylığı için varsayılan

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isBusy = false;

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

    /// <summary>Başarılı giriş sonrası App.axaml.cs tarafından dinlenir.</summary>
    public event EventHandler? LoginSuccessful;
}
