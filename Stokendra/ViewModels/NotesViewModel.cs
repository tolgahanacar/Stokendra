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
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private Note?   _selectedNote;

    // Edit properties
    [ObservableProperty] private string _editTitle   = "";
    [ObservableProperty] private string _editContent = "";
    [ObservableProperty] private DateTimeOffset _editCreatedAt = DateTimeOffset.Now;
    [ObservableProperty] private bool   _isEditing;
    private int _editingId = 0;

    public BulkObservableCollection<Note> Notes { get; } = new();
    public BulkObservableCollection<Note> SelectedNotes { get; } = new();

    public bool HasSelection => SelectedNotes.Count > 0 || SelectedNote != null;

    public NotesViewModel(INoteRepository notes, IDialogService dialogService)
    {
        _notes = notes;
        _dialogService = dialogService;

        DataEvents.DataChanged += OnDataChanged;

        SelectedNotes.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasSelection));
        AsyncHelper.RunSafe(
            () => LoadAsync(),
            ex => { AppLogger.LogError("Notes load failed", ex); StatusText = $"{LocalizationManager.L("error")}: {ex.Message}"; });
    }

    private bool _isLoaded;

    private void OnDataChanged(DataDomain domain)
    {
        if (domain == DataDomain.Notes)
        {
            _isLoaded = false;
        }
    }

    public async Task EnsureLoadedAsync()
    {
        if (_isLoaded) return;
        await LoadAsync();
    }

    partial void OnSelectedNoteChanged(Note? value) => OnPropertyChanged(nameof(HasSelection));

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var data = await _notes.GetAllAsync();
            Notes.ReplaceAll(data);
            _isLoaded = true;
            StatusText = LocalizationManager.L("records_info", data.Count, 0).Split('•')[0].Trim();
        }
        catch (Exception ex) 
        { 
            AppLogger.LogError("Notes load error", ex);
            StatusText = LocalizationManager.L("error"); 
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public void NewNote()
    {
        if (!EnsureCanEditForSession()) return;
        _editingId   = 0;
        EditTitle    = "";
        EditContent  = "";
        EditCreatedAt = DateTimeOffset.Now;
        IsEditing    = true;
    }

    [RelayCommand]
    public async Task EditNoteAsync(System.Collections.IList? items)
    {
        if (!EnsureCanEditForSession()) return;
        var list = items?.OfType<Note>().ToList() ?? SelectedNotes.ToList();
        if (list.Count == 0 && SelectedNote != null) list.Add(SelectedNote);
        if (list.Count == 0) return;

        if (list.Count >= 2)
        {
            var vm = new BulkEditNotesViewModel(list, _notes, _dialogService);
            bool? success = await _dialogService.ShowDialogAsync(vm);
            if (success == true)
            {
                DataEvents.NotifyChanged(DataDomain.Notes);
                await LoadAsync();
                StatusText = LocalizationManager.L("bulk_edit_success", list.Count);
            }
        }
        else
        {
            var note = list.First();
            _editingId = note.Id;
            EditTitle = note.Title;
            EditContent = note.Content;
            EditCreatedAt = SafeDateTimeOffset(note.CreatedAt);
            IsEditing = true;
        }
    }

    private static DateTimeOffset SafeDateTimeOffset(DateTime dt)
    {
        if (dt <= DateTime.MinValue.AddDays(2) || dt >= DateTime.MaxValue.AddDays(-2))
            return DateTimeOffset.Now;

        try { return new DateTimeOffset(dt); }
        catch { return DateTimeOffset.Now; }
    }

    [RelayCommand]
    public async Task SaveNoteAsync()
    {
        if (!EnsureCanEditForSession()) return;
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
                await _notes.AddAsync(n);
                StatusText = LocalizationManager.L("note_saved_success");
            }
            else
            {
                var n = new Note { Id = _editingId, Title = EditTitle.Trim(), Content = EditContent.Trim(), CreatedAt = EditCreatedAt.DateTime };
                await _notes.UpdateAsync(n);
                StatusText = LocalizationManager.L("note_saved_success");
            }
            IsEditing = false;
            DataEvents.NotifyChanged(DataDomain.Notes);
            await LoadAsync();
        }
        catch (Exception ex) 
        { 
            AppLogger.LogError("Save note error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), $"{LocalizationManager.L("error")}: {ex.Message}");
        }
    }

    [RelayCommand]
    public void CancelEdit() => IsEditing = false;

    [RelayCommand]
    public async Task DeleteSelectedAsync(System.Collections.IList? items)
    {
        if (!EnsureCanEditForSession()) return;
        var list = items?.OfType<Note>().ToList() ?? SelectedNotes.ToList();
        if (list.Count == 0 && SelectedNote != null) list.Add(SelectedNote);
        if (list.Count == 0) return;

        bool confirm;
        if (list.Count >= 2)
        {
            confirm = await _dialogService.ShowConfirmAsync(
                LocalizationManager.L("confirm_bulk_delete_title"),
                LocalizationManager.L("confirm_bulk_note_delete", list.Count));
        }
        else
        {
            confirm = await _dialogService.ShowConfirmAsync(
                LocalizationManager.L("confirm_delete_title"),
                LocalizationManager.L("confirm_note_delete"));
        }

        if (!confirm) return;

        try
        {
            await _notes.DeleteBulkAsync(list.Select(x => x.Id).ToList());
            IsEditing = false;
            SelectedNote = null;
            SelectedNotes.Clear();
            DataEvents.NotifyChanged(DataDomain.Notes);
            await LoadAsync();
            StatusText = LocalizationManager.L("bulk_delete_success", list.Count);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Notes delete error", ex);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), ex.Message);
        }
    }

    [RelayCommand]
    public async Task ExportExcelAsync()
    {
        string baseName = LocalizationManager.L("export_fn_notes");
        string fileName = $"{baseName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string? path = await _dialogService.SaveFileAsync(
            LocalizationManager.L("export_excel"), 
            fileName, 
            LocalizationManager.L("svc_excel_filter"));
            
        if (string.IsNullOrEmpty(path)) return;

        var snapshot = Notes.ToList();
        if (snapshot.Count == 0)
        {
            await _dialogService.ShowMessageAsync(LocalizationManager.L("warning"), LocalizationManager.L("bulk_operation_empty"));
            return;
        }

        IsLoading = true;
        try
        {
            StatusText = LocalizationManager.L("loading");
            await Task.Run(() => {
                var headers = new[] { 
                    "ID", 
                    LocalizationManager.L("title"), 
                    LocalizationManager.L("description"), 
                    LocalizationManager.L("created_date_header") 
                };
                ExcelService.ExportToExcel(
                    path, 
                    LocalizationManager.L("notes"), 
                    headers, 
                    snapshot, 
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
            AppLogger.LogError("Notes Excel export error", ex);
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

    [RelayCommand]
    public async Task ImportExcelAsync()
    {
        if (!EnsureCanEditForSession()) return;
        string? path = await _dialogService.OpenFileAsync(LocalizationManager.L("svc_excel_select_title"), LocalizationManager.L("svc_excel_filter"));
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            StatusText = LocalizationManager.L("loading");
            int count = await Task.Run(async () => {
                var mappings = new System.Collections.Generic.Dictionary<string, string[]>
                {
                    { "title", new[] { "başlık", "baslik", "title", "başlik" } },
                    { "content", new[] { "açıklama", "aciklama", "içerik", "icerik", "description", "content" } },
                    { "date", new[] { "oluşturulma tarihi", "olusturulma tarihi", "tarih", "date", "created_date_header", "created date" } }
                };

                var fallback = new System.Collections.Generic.Dictionary<string, int>
                {
                    { "title", 2 }, { "content", 3 }, { "date", 4 }
                };

                var toImport = Stokendra.Infrastructure.ExcelImportHelper.ImportData(path, mappings, fallback, (row, col) => 
                {
                    string title = col["title"] > 0 ? Stokendra.Infrastructure.ExcelImportHelper.ParseCellString(row.Cell(col["title"])) : "";
                    if (string.IsNullOrEmpty(title)) return null;

                    string content = col["content"] > 0 ? Stokendra.Infrastructure.ExcelImportHelper.ParseCellString(row.Cell(col["content"])) : "";
                    
                    DateTime date = col["date"] > 0
                        ? Stokendra.Infrastructure.ExcelImportHelper.ParseCellDate(row.Cell(col["date"]))
                        : DateTime.Now;

                    return new Note {
                        Title = title,
                        Content = content,
                        CreatedAt = date,
                        UpdatedAt = DateTime.Now,
                        Date = date
                    };
                });
                
                if (toImport.Count > 0) await _notes.AddBulkAsync(toImport);
                return toImport.Count;
            });
            DataEvents.NotifyChanged(DataDomain.Notes);
            await LoadAsync();
            StatusText = LocalizationManager.L("import_success", count);
            await _dialogService.ShowMessageAsync(LocalizationManager.L("info"), LocalizationManager.L("import_success", count));
        }
        catch (Exception ex) 
        { 
            AppLogger.LogError("Notes Excel import error", ex);
            StatusText = $"{LocalizationManager.L("error")}: {ex.Message}"; 
            await _dialogService.ShowMessageAsync(LocalizationManager.L("error"), LocalizationManager.L("import_error", ex.Message));
        }
    }

    public override void RefreshSession()
    {
        base.RefreshSession();
        IsEditing = false;
        _editingId = 0;
        EditTitle = "";
        EditContent = "";
        SelectedNote = null;
        SelectedNotes.Clear();
        _isLoaded = false;
    }
}
