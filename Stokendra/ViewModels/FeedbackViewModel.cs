using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra;
using Stokendra.Infrastructure;
using Stokendra.Models;
using Stokendra.Services;

namespace Stokendra.ViewModels;

public partial class FeedbackViewModel : ViewModelBase
{
    private readonly IFeedbackService _feedbackService;
    private readonly IDialogService _dialogService;

    [ObservableProperty] private ObservableCollection<string> _feedbackTypes = new()
    {
        LocalizationManager.L("feedback_type_bug"),
        LocalizationManager.L("feedback_type_feature"),
        LocalizationManager.L("feedback_type_question")
    };

    [ObservableProperty] private string _selectedType = "";
    [ObservableProperty] private string _subject = "";
    [ObservableProperty] private string _userEmail = "";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private bool _includeLogs = true;
    [ObservableProperty] private bool _isSending;
    [ObservableProperty] private string _targetEmailDisplay = "";
    [ObservableProperty] private string _systemInfoPreview = "";
    [ObservableProperty] private string _recentLogsPreview = "";

    public FeedbackViewModel(IFeedbackService feedbackService, IDialogService dialogService)
    {
        _feedbackService = feedbackService;
        _dialogService = dialogService;

        RefreshSession();
    }

    public override void RefreshSession()
    {
        base.RefreshSession();
        int previousIndex = FeedbackTypes.IndexOf(SelectedType);

        FeedbackTypes = new ObservableCollection<string>
        {
            LocalizationManager.L("feedback_type_bug"),
            LocalizationManager.L("feedback_type_feature"),
            LocalizationManager.L("feedback_type_question")
        };

        SelectedType = (previousIndex >= 0 && previousIndex < FeedbackTypes.Count)
            ? FeedbackTypes[previousIndex]
            : FeedbackTypes[0];

        TargetEmailDisplay = _feedbackService.TargetEmail;
        RefreshDiagnostics();
    }

    [RelayCommand]
    public void RefreshDiagnostics()
    {
        SystemInfoPreview = AppLogger.GetSystemDiagnosticSummary();
        RecentLogsPreview = AppLogger.GetRecentLogs(25);
    }

    [RelayCommand]
    public async Task SendFeedbackAsync()
    {
        if (string.IsNullOrWhiteSpace(Subject) || string.IsNullOrWhiteSpace(Message))
        {
            await _dialogService.ShowMessageAsync(
                LocalizationManager.L("warning"),
                LocalizationManager.L("feedback_validation_error"));
            return;
        }

        IsSending = true;
        try
        {
            var report = BuildCurrentReport();
            var (success, resultMsg) = await _feedbackService.SendFeedbackAsync(report);

            if (success)
            {
                await _dialogService.ShowMessageAsync(LocalizationManager.L("info"), resultMsg);
                // Başarılıysa formu temizle
                Subject = "";
                Message = "";
            }
            else
            {
                await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), resultMsg);
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("SendFeedbackAsync error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), ex.Message);
        }
        finally
        {
            IsSending = false;
        }
    }

    [RelayCommand]
    public void OpenMailClient()
    {
        if (string.IsNullOrWhiteSpace(Subject))
            Subject = $"{SelectedType} - {LocalizationManager.L("feedback_default_subject")}";

        var report = BuildCurrentReport();
        _feedbackService.OpenDefaultMailClient(report);
    }

    [RelayCommand]
    public async Task CopyReportToClipboardAsync()
    {
        var report = BuildCurrentReport();
        string fullText = _feedbackService.BuildFullReportText(report);

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard != null)
        {
            await desktop.MainWindow.Clipboard.SetTextAsync(fullText);
            await _dialogService.ShowMessageAsync(
                LocalizationManager.L("info"),
                LocalizationManager.L("feedback_copied_to_clipboard"));
        }
    }

    private FeedbackReport BuildCurrentReport()
    {
        return new FeedbackReport
        {
            Type = SelectedType,
            Subject = Subject.Trim(),
            UserEmail = UserEmail.Trim(),
            Message = Message.Trim(),
            IncludeLogs = IncludeLogs,
            SystemInfo = AppLogger.GetSystemDiagnosticSummary(),
            RecentLogs = IncludeLogs ? AppLogger.GetRecentLogs(50) : "",
            CreatedAt = DateTime.Now
        };
    }
}
