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

            // ViewModel DI'dan alınır, View'a DataContext olarak verilir
            var loginVm = ServiceContainer.GetService<LoginViewModel>();
            var loginView = new LoginView { DataContext = loginVm };

            loginVm.LoginSuccessful += (_, _) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var mainVm = ServiceContainer.GetService<MainViewModel>();
                    var mainWindow = new MainWindow { DataContext = mainVm };

                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                    loginView.Close();
                });
            };

            desktop.MainWindow = loginView;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
