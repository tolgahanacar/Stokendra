using CommunityToolkit.Mvvm.ComponentModel;
using Stokendra.Data;
using System;
using System.Reflection;
using System.IO;

namespace Stokendra.ViewModels;

public partial class GuideViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly Database _database;

    [ObservableProperty] private int _selectedTab = 0; // 0: User Guide, 1: Developer Roadmap
    [ObservableProperty] private string _appVersion = "";
    [ObservableProperty] private string _databasePath = "";
    [ObservableProperty] private string _settingsPath = "";

    public GuideViewModel(AppSettings settings, Database database)
    {
        _settings = settings;
        _database = database;

        AppVersion = AppInfo.VersionString;

        DatabasePath = _database.DatabasePath;
        SettingsPath = AppPaths.SettingsFilePath;
    }
}
