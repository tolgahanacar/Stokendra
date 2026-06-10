using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Stokendra.Data;
using Stokendra.Data.Interfaces;
using Stokendra.Data.Repositories;
using Stokendra.Infrastructure;
using Stokendra.Services;
using Stokendra.ViewModels;
using ReactiveUI;
using System.Reactive;
using System;

namespace Stokendra;

class Program
{
    [STAThread]
    public static void Main(string[] args) 
    {
        // Global Exception Handling
        AppDomain.CurrentDomain.UnhandledException += (s, e) => 
        {
            AppLogger.LogError("FATAL: AppDomain Unhandled Exception", e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (s, e) => 
        {
            AppLogger.LogError("FATAL: TaskScheduler Unobserved Exception", e.Exception);
            e.SetObserved();
        };

        RxApp.DefaultExceptionHandler = Observer.Create<Exception>(ex => 
        {
            AppLogger.LogError("FATAL: ReactiveUI Exception", ex);
        });

        // Migrate legacy settings and database if needed
        AppPaths.MigrateLegacyData();

        ServiceProvider? container = null;
        try 
        {
            container = SetupDI();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("FATAL: Startup Exception", ex);
            // Kritik hata durumunda kullanıcıya bilgi verilebilir
            throw;
        }
        finally
        {
            container?.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
    }

    private static ServiceProvider SetupDI()
    {
        var services = new ServiceCollection();



        // ── Data ──────────────────────────────────────────────────────────
        var settings = AppSettings.Yukle();
        LocalizationManager.Initialize(settings.Language);
        string dbPath = !string.IsNullOrWhiteSpace(settings.DbPath) ? settings.DbPath : AppPaths.DefaultDatabasePath;
        var database  = new Database(dbPath);

        services.AddSingleton<IDbConnectionFactory>(_ => new SqliteConnectionFactory(database.DatabasePath));
        services.AddSingleton(database);
        services.AddSingleton(settings);

        // ── Repositories ──────────────────────────────────────────────────
        services.AddSingleton<IStockCardRepository,    StockCardRepository>();
        services.AddSingleton<IMovementRepository,     MovementRepository>();
        services.AddSingleton<IUserRepository,         UserRepository>();
        services.AddSingleton<INoteRepository,         NoteRepository>();
        services.AddSingleton<IReportRepository,       ReportRepository>();
        services.AddSingleton<IServiceRecordRepository, ServiceRecordRepository>();
        services.AddSingleton<IDepartmentRepository,   DepartmentRepository>();
        services.AddSingleton<IConfigRepository,       ConfigRepository>();

        // ── Services ──────────────────────────────────────────────────────
        services.AddSingleton<IStockCardService, StockCardService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<AppServices>();

        // ── ViewModels ────────────────────────────────────────────────────
        // Ana Sayfalar ve Kalıcı Durum Gerektirenler (Singleton)
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<StockCardsViewModel>();
        services.AddSingleton<StockMovementsViewModel>();
        services.AddSingleton<ServicesViewModel>();
        services.AddSingleton<NotesViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<StocksViewModel>();
        services.AddSingleton<DepartmentsViewModel>();
        services.AddSingleton<ReportsViewModel>();
        services.AddSingleton<GuideViewModel>();
        services.AddSingleton<UsersViewModel>();

        // Geçici Pencereler ve Formlar (Transient)
        services.AddTransient<LoginViewModel>();
        services.AddTransient<BulkMovementViewModel>();
        services.AddTransient<AddMovementViewModel>();
        services.AddTransient<AddStockCardViewModel>();
        services.AddTransient<AddServiceViewModel>();
        services.AddTransient<StockCardDetailViewModel>();
        services.AddTransient<PrintPreviewViewModel>();
        services.AddTransient<PromptViewModel>();

        // ── Infrastructure ────────────────────────────────────────────────
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ILogger, Logger>();

        var container = services.BuildServiceProvider();
        ServiceContainer.Initialize(container);

        // AppServices singleton'ını başlat
        container.GetRequiredService<AppServices>();
        
        return container;
    }
}
