using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using Stokendra.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Stokendra.ViewModels;

public partial class BulkEditNotesViewModel : ViewModelBase
{
    private readonly INoteRepository _notes;
    private readonly IDialogService _dialogService;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _errorMessage = "";

    // Bulk apply options
    [ObservableProperty] private bool _isApplyDate;
    [ObservableProperty] private DateTime? _date = DateTime.Today;

    [ObservableProperty] private bool _isApplyTitlePrefix;
    [ObservableProperty] private string _titlePrefix = "";

    [ObservableProperty] private bool _isApplyContent;
    [ObservableProperty] private string _contentAppend = "";

    public ObservableCollection<Note> EditingNotes { get; } = new();

    public Action<bool>? CloseAction { get; set; }

    public BulkEditNotesViewModel(
        List<Note> selectedNotes,
        INoteRepository notes,
        IDialogService dialogService)
    {
        _notes = notes;
        _dialogService = dialogService;
        Title = LocalizationManager.L("bulk_edit_title", selectedNotes.Count);

        foreach (var n in selectedNotes)
        {
            EditingNotes.Add(new Note
            {
                Id = n.Id,
                Title = n.Title,
                Content = n.Content,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt
            });
        }

        Date = selectedNotes.FirstOrDefault()?.CreatedAt ?? DateTime.Today;
    }

    [RelayCommand]
    private void ApplyBulk()
    {
        ErrorMessage = "";
        int updated = 0;

        foreach (var n in EditingNotes)
        {
            if (IsApplyDate && Date.HasValue)
            {
                n.CreatedAt = Date.Value;
                updated++;
            }
            if (IsApplyTitlePrefix && !string.IsNullOrWhiteSpace(TitlePrefix))
            {
                if (!n.Title.StartsWith(TitlePrefix.Trim()))
                {
                    n.Title = $"{TitlePrefix.Trim()} {n.Title}";
                    updated++;
                }
            }
            if (IsApplyContent && !string.IsNullOrWhiteSpace(ContentAppend))
            {
                n.Content = string.IsNullOrWhiteSpace(n.Content)
                    ? ContentAppend.Trim()
                    : $"{n.Content}\n{ContentAppend.Trim()}";
                updated++;
            }
        }

        if (updated > 0)
        {
            var temp = EditingNotes.ToList();
            EditingNotes.Clear();
            foreach (var item in temp) EditingNotes.Add(item);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = "";
        try
        {
            foreach (var n in EditingNotes)
            {
                if (string.IsNullOrWhiteSpace(n.Title))
                {
                    ErrorMessage = $"⚠️ {LocalizationManager.L("title_empty")}";
                    return;
                }
            }

            foreach (var n in EditingNotes)
            {
                n.UpdatedAt = DateTime.Now;
                await _notes.UpdateAsync(n);
            }

            CloseAction?.Invoke(true);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Bulk notes edit error", ex);
            ErrorMessage = $"⚠️ {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel() => CloseAction?.Invoke(false);
}
