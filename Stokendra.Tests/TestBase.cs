using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using Moq;
using Stokendra.Data;
using Stokendra.Data.Interfaces;
using Stokendra.Data.Repositories;
using Stokendra.Infrastructure;
using System;
using System.IO;

namespace Stokendra.Tests;

public abstract class TestBase : IDisposable
{
    protected readonly SqliteConnection Connection;
    protected readonly Database Database;
    protected readonly Mock<IDialogService> MockDialog;
    protected readonly Mock<ILogger> MockLogger;
    protected readonly IServiceProvider ServiceProvider;

    protected TestBase()
    {
        // 1. Database Setup
        string dbPath = Path.Combine(Path.GetTempPath(), $"stokendra_test_{Guid.NewGuid()}.db");
        Database = new Database(dbPath);
        Connection = new SqliteConnection($"Data Source={dbPath}");
        Connection.Open();
        SqliteHelpers.RegisterCustomFunctions(Connection);

        // 2. Mocks
        MockDialog = new Mock<IDialogService>();
        MockLogger = new Mock<ILogger>();

        // 3. DI Setup for tests
        var services = new ServiceCollection();
        var factory = new TestDbFactory(dbPath);
        
        services.AddSingleton<IDbConnectionFactory>(factory);
        services.AddSingleton<IStockCardRepository>(new StockCardRepository(factory));
        services.AddSingleton<IMovementRepository>(new MovementRepository(factory));
        services.AddSingleton<IDepartmentRepository>(new DepartmentRepository(factory));
        services.AddSingleton<IConfigRepository>(new ConfigRepository(factory));
        services.AddSingleton<IReportRepository>(new ReportRepository(factory));
        services.AddSingleton<IServiceRecordRepository>(new ServiceRecordRepository(factory));
        services.AddSingleton<INoteRepository>(new NoteRepository(factory));
        services.AddSingleton<IDialogService>(MockDialog.Object);
        services.AddSingleton<ILogger>(MockLogger.Object);

        ServiceProvider = services.BuildServiceProvider();
        ServiceContainer.Initialize(ServiceProvider);

        // 4. AppServices Setup
        new AppServices(new AppSettings(), Database);
        LocalizationManager.Initialize("en");
    }

    public void Dispose()
    {
        Database.Dispose();
        Connection.Close();
        Connection.Dispose();
        
        // SQLite bağlantı havuzlarını temizle ki dosya kilidi kalksın
        SqliteConnection.ClearAllPools();
        
        if (File.Exists(Database.DatabasePath))
        {
            try { File.Delete(Database.DatabasePath); } catch { /* Ignore */ }
        }
    }
}
