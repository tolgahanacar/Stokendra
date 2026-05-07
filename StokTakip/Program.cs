using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using StokTakip.Data;
using StokTakip.Data.Interfaces;
using StokTakip.Data.Repositories;
using StokTakip.Infrastructure;
using StokTakip.Services;
using StokTakip.ViewModels;

namespace StokTakip;

class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    {
        SetupDI();
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
    }

    private static void SetupDI()
    {
        var services = new ServiceCollection();

        // ── Data ──────────────────────────────────────────────────────────
        var settings = AppSettings.Yukle();
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
        services.AddSingleton<AppServices>();

        // ── ViewModels ────────────────────────────────────────────────────
        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<StockCardsViewModel>();
        services.AddSingleton<StockMovementsViewModel>();
        services.AddSingleton<ServicesViewModel>();
        services.AddSingleton<NotesViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<StocksViewModel>();
        services.AddSingleton<MainViewModel>();

        var container = services.BuildServiceProvider();
        ServiceContainer.Initialize(container);

        // AppServices singleton'ını başlat
        container.GetRequiredService<AppServices>();
    }
}
