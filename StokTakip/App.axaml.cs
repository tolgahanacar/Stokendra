using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using StokTakip.Infrastructure;
using StokTakip.ViewModels;
using StokTakip.Views;

namespace StokTakip;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;

            var loginVm = ServiceContainer.GetService<LoginViewModel>();
            var loginView = new LoginView { DataContext = loginVm };

            void OnLoginSuccessful(object? sender, EventArgs e)
            {
                loginVm.LoginSuccessful -= OnLoginSuccessful; // Unsubscribe to prevent memory leak
                Dispatcher.UIThread.Post(() =>
                {
                    var mainVm = ServiceContainer.GetService<MainViewModel>();
                    var mainWindow = new MainWindow { DataContext = mainVm };

                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                    loginView.Close();
                });
            }

            loginVm.LoginSuccessful += OnLoginSuccessful;

            desktop.MainWindow = loginView;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
