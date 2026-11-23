using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data;
using Stokendra.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Stokendra.ViewModels;

public sealed class GuideCardItem
{
    public string Icon { get; init; } = "📌";
    public string Tag { get; init; } = "";
    public string TagColor { get; init; } = "#38BDF8";
    public string TagBg { get; init; } = "#102A45";
    public string Title { get; init; } = "";
    public string Content { get; init; } = "";
    public bool IsDev { get; init; }
}

public partial class GuideViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly Database _database;

    [ObservableProperty] private int _selectedTab = 0; // 0: User Guide, 1: Developer Roadmap
    [ObservableProperty] private string _appVersion = "";
    [ObservableProperty] private string _databasePath = "";
    [ObservableProperty] private string _settingsPath = "";
    [ObservableProperty] private string _searchText = "";

    private readonly List<GuideCardItem> _userCards = new();
    private readonly List<GuideCardItem> _devCards = new();

    public ObservableCollection<GuideCardItem> FilteredUserCards { get; } = new();
    public ObservableCollection<GuideCardItem> FilteredDevCards { get; } = new();

    [ObservableProperty] private bool _hasUserResults = true;
    [ObservableProperty] private bool _hasDevResults = true;

    public GuideViewModel(AppSettings settings, Database database)
    {
        _settings = settings;
        _database = database;

        AppVersion = AppInfo.VersionString;
        DatabasePath = _database.DatabasePath;
        SettingsPath = AppPaths.SettingsFilePath;

        LoadGuideCards();
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchText = "";
    }

    private void LoadGuideCards()
    {
        _userCards.Clear();
        _devCards.Clear();

        // 👥 Kullanıcı Rehberi Kartları
        _userCards.Add(new GuideCardItem
        {
            Icon = "🚀",
            Tag = "TEMEL İŞ AKIŞI",
            TagColor = "#FFD700",
            TagBg = "#332A00",
            Title = LocalizationManager.L("guide_user_quickstart_title"),
            Content = LocalizationManager.L("guide_user_quickstart_desc")
        });

        _userCards.Add(new GuideCardItem
        {
            Icon = "📇",
            Tag = "ENVANTER",
            TagColor = "#38AEFF",
            TagBg = "#0A2540",
            Title = LocalizationManager.L("guide_user_stockcards_title"),
            Content = LocalizationManager.L("guide_user_stockcards_desc")
        });

        _userCards.Add(new GuideCardItem
        {
            Icon = "🔄",
            Tag = "HAREKETLER",
            TagColor = "#00DC82",
            TagBg = "#063720",
            Title = LocalizationManager.L("guide_user_movements_title"),
            Content = LocalizationManager.L("guide_user_movements_desc")
        });

        _userCards.Add(new GuideCardItem
        {
            Icon = "🛠️",
            Tag = "SERVİS TAKİBİ",
            TagColor = "#F39C12",
            TagBg = "#3D2403",
            Title = LocalizationManager.L("guide_user_services_title"),
            Content = LocalizationManager.L("guide_user_services_desc")
        });

        _userCards.Add(new GuideCardItem
        {
            Icon = "📝",
            Tag = "NOTLAR",
            TagColor = "#A78BFA",
            TagBg = "#2E1065",
            Title = LocalizationManager.L("guide_user_notes_title"),
            Content = LocalizationManager.L("guide_user_notes_desc")
        });

        _userCards.Add(new GuideCardItem
        {
            Icon = "👥",
            Tag = "YETKİLENDİRME",
            TagColor = "#38BDF8",
            TagBg = "#082F49",
            Title = LocalizationManager.L("guide_user_users_title"),
            Content = LocalizationManager.L("guide_user_users_desc")
        });

        _userCards.Add(new GuideCardItem
        {
            Icon = "💾",
            Tag = "GÜVENLİK & YEDEK",
            TagColor = "#C084FC",
            TagBg = "#3B0764",
            Title = LocalizationManager.L("guide_user_backup_title"),
            Content = LocalizationManager.L("guide_user_backup_desc")
        });

        _userCards.Add(new GuideCardItem
        {
            Icon = "📊",
            Tag = "RAPORLAMA",
            TagColor = "#FB923C",
            TagBg = "#431407",
            Title = LocalizationManager.L("guide_user_reports_title"),
            Content = LocalizationManager.L("guide_user_reports_desc")
        });

        _userCards.Add(new GuideCardItem
        {
            Icon = "⚡",
            Tag = "CANLI GÜNCELLEME",
            TagColor = "#34D399",
            TagBg = "#064E3B",
            Title = LocalizationManager.L("guide_user_updates_title"),
            Content = LocalizationManager.L("guide_user_updates_desc")
        });

        // 💻 Geliştirici & Mimari Kartları
        _devCards.Add(new GuideCardItem
        {
            Icon = "🚀",
            Tag = "YENİLİK (v7.5.0)",
            TagColor = "#F43F5E",
            TagBg = "#4C0519",
            Title = LocalizationManager.L("guide_dev_roadmap_title"),
            Content = LocalizationManager.L("guide_dev_roadmap_desc"),
            IsDev = true
        });

        _devCards.Add(new GuideCardItem
        {
            Icon = "⚡",
            Tag = ".NET 10 & PERFORMANS",
            TagColor = "#FFD700",
            TagBg = "#332A00",
            Title = LocalizationManager.L("guide_dev_perf_title"),
            Content = LocalizationManager.L("guide_dev_perf_desc"),
            IsDev = true
        });

        _devCards.Add(new GuideCardItem
        {
            Icon = "🏗️",
            Tag = "MVVM MİMARİSİ",
            TagColor = "#38AEFF",
            TagBg = "#0A2540",
            Title = LocalizationManager.L("guide_dev_mvvm_title"),
            Content = LocalizationManager.L("guide_dev_mvvm_desc"),
            IsDev = true
        });

        _devCards.Add(new GuideCardItem
        {
            Icon = "📂",
            Tag = "PROJE HİYERARŞİSİ",
            TagColor = "#00DC82",
            TagBg = "#063720",
            Title = LocalizationManager.L("guide_dev_folders_title"),
            Content = LocalizationManager.L("guide_dev_folders_desc"),
            IsDev = true
        });

        _devCards.Add(new GuideCardItem
        {
            Icon = "🌐",
            Tag = "ÇOKLU DİL",
            TagColor = "#F39C12",
            TagBg = "#3D2403",
            Title = LocalizationManager.L("guide_dev_lang_title"),
            Content = LocalizationManager.L("guide_dev_lang_desc"),
            IsDev = true
        });

        _devCards.Add(new GuideCardItem
        {
            Icon = "🗄️",
            Tag = "VERİTABANI & GÖÇ",
            TagColor = "#9B59B6",
            TagBg = "#2E1065",
            Title = LocalizationManager.L("guide_dev_db_title"),
            Content = LocalizationManager.L("guide_dev_db_desc"),
            IsDev = true
        });

        _devCards.Add(new GuideCardItem
        {
            Icon = "📊",
            Tag = "EXCEL MOTORU",
            TagColor = "#38BDF8",
            TagBg = "#082F49",
            Title = LocalizationManager.L("guide_dev_excel_title"),
            Content = LocalizationManager.L("guide_dev_excel_desc"),
            IsDev = true
        });
    }

    private void ApplyFilter()
    {
        string query = SearchText?.Trim() ?? "";

        FilteredUserCards.Clear();
        foreach (var card in _userCards)
        {
            if (string.IsNullOrEmpty(query) || Matches(card, query))
                FilteredUserCards.Add(card);
        }
        HasUserResults = FilteredUserCards.Count > 0;

        FilteredDevCards.Clear();
        foreach (var card in _devCards)
        {
            if (string.IsNullOrEmpty(query) || Matches(card, query))
                FilteredDevCards.Add(card);
        }
        HasDevResults = FilteredDevCards.Count > 0;
    }

    private static bool Matches(GuideCardItem card, string query)
    {
        return card.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || card.Content.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || card.Tag.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }
}
