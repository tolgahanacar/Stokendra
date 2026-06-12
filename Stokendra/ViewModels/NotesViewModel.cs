using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Data.Interfaces;
using Stokendra.Models;
using Stokendra.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Stokendra.ViewModels;

public partial class NotesViewModel : ViewModelBase
{
    private readonly INoteRepository _notes;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private Note?   _selectedNote;

    // Edit properties
    [ObservableProperty] private string _editTitle   = "";
    [ObservableProperty] private string _editContent = "";
    [ObservableProperty] private DateTimeOffset _editCreatedAt = DateTimeOffset.Now;
    [ObservableProperty] private bool   _isEditing;
    private int _editingId = 0;

    public ObservableCollection<Note> Notes { get; } = new();
    public ObservableCollection<Note> SelectedNotes { get; } = new();

    public bool HasSelection => SelectedNotes.Count > 0 || SelectedNote != null;

    public NotesViewModel(INoteRepository notes, IDialogService dialogService, ILogger logger)
    {
        _notes = notes;
        _dialogService = dialogService;
        _logger = logger;
        SelectedNotes.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasSelection));
        _ = LoadAsync();
    }

    partial void OnSelectedNoteChanged(Note? value) => OnPropertyChanged(nameof(HasSelection));

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var data = await Task.Run(() => _notes.GetAll());
            Notes.Clear();
            foreach (var n in data) Notes.Add(n);
            StatusText = LocalizationManager.L("records_info", data.Count, 0).Split('•')[0].Trim();
        }
        catch (Exception ex) 
        { 
            _logger.LogError("Notes load error", ex);
            StatusText = LocalizationManager.L("error"); 
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public void NewNote()
    {
        _editingId   = 0;
        EditTitle    = "";
        EditContent  = "";
        EditCreatedAt = DateTimeOffset.Now;
        IsEditing    = true;
    }

    [RelayCommand]
    public void EditNote()
    {
        if (SelectedNote == null) return;
        _editingId   = SelectedNote.Id;
        EditTitle    = SelectedNote.Title;
        EditContent  = SelectedNote.Content;
        EditCreatedAt = new DateTimeOffset(SelectedNote.CreatedAt);
        IsEditing    = true;
    }

    [RelayCommand]
    public async Task SaveNoteAsync()
    {
        if (string.IsNullOrWhiteSpace(EditTitle))
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("title_empty"));
            return;
        }

        try
        {
            if (_editingId == 0)
            {
                var n = new Note { Title = EditTitle.Trim(), Content = EditContent.Trim(), CreatedAt = EditCreatedAt.DateTime };
                await Task.Run(() => _notes.Add(n));
                StatusText = LocalizationManager.L("save_settings");
            }
            else
            {
                var n = new Note { Id = _editingId, Title = EditTitle.Trim(), Content = EditContent.Trim(), CreatedAt = EditCreatedAt.DateTime };
                await Task.Run(() => _notes.Update(n));
                StatusText = LocalizationManager.L("save_settings");
            }
            IsEditing = false;
            await LoadAsync();
        }
        catch (Exception ex) 
        { 
            _logger.LogError("Save note error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
        }
    }

    [RelayCommand]
    public void CancelEdit() => IsEditing = false;

    [RelayCommand]
    public async Task DeleteSelectedAsync(System.Collections.IList? items)
    {
        var list = items?.Cast<Note>().ToList() ?? new List<Note>();
        if (list.Count == 0 && SelectedNote != null) list.Add(SelectedNote);
        if (list.Count == 0) return;

        bool confirm = await _dialogService.ShowConfirmAsync(LocalizationManager.L("confirm_delete_title"), LocalizationManager.L("confirm_note_delete"));
        if (!confirm) return;

        try {
            await Task.Run(() => _notes.DeleteBulk(list.Select(x => x.Id).ToList()));
            await LoadAsync();
            StatusText = LocalizationManager.L("bulk_delete_success", list.Count);
        } catch (Exception ex) { await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), ex.Message); }
    }

    [RelayCommand]
    public async Task ExportExcelAsync()
    {
        string fileName = $"Notlar_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string? path = await _dialogService.SaveFileAsync(
            LocalizationManager.L("export_excel"), 
            fileName, 
            "Excel File (*.xlsx)|*.xlsx");
            
        if (string.IsNullOrEmpty(path)) return;

        IsLoading = true;
        try
        {
            StatusText = LocalizationManager.L("loading");
            await Task.Run(() => {
                var headers = new[] { "ID", "Başlık", "Açıklama", "Oluşturma Tarihi" };
                ExcelService.ExportToExcel(
                    path, 
                    "Notlar", 
                    headers, 
                    Notes, 
                    n => new object?[] { 
                        n.Id, 
                        n.Title, 
                        n.Content, 
                        n.CreatedAt.ToString("dd.MM.yyyy HH:mm") 
                    },
                    sheet => {
                        var colTitle = sheet.Column(2);
                        colTitle.Width = 40;
                        colTitle.Style.Alignment.WrapText = true;

                        var colContent = sheet.Column(3);
                        colContent.Width = 90;
                        colContent.Style.Alignment.WrapText = true;
                    });
            });
            StatusText = LocalizationManager.L("export_success", path);
            await _dialogService.ShowMessageAsync(
                LocalizationManager.L("info"), 
                LocalizationManager.L("export_success", path));
        }
        catch (Exception ex)
        {
            _logger.LogError("Notes Excel export error", ex);
            StatusText = $"{LocalizationManager.L("error")}: {ex.Message}";
            await _dialogService.ShowMessageAsync(
                LocalizationManager.L("error"), 
                $"{LocalizationManager.L("export_error")}: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
