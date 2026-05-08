using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using StokTakip.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace StokTakip.ViewModels;

public partial class NotesViewModel : ViewModelBase
{
    private readonly INoteRepository _notes;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private Not?   _selectedNote;

    // Yeni/düzenleme alanları
    [ObservableProperty] private string _editTitle   = "";
    [ObservableProperty] private string _editContent = "";
    [ObservableProperty] private bool   _isEditing;
    private int _editingId = 0;

    public ObservableCollection<Not> Notes { get; } = new();

    public NotesViewModel(INoteRepository notes, IDialogService dialogService, ILogger logger)
    {
        _notes = notes;
        _dialogService = dialogService;
        _logger = logger;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var data = await Task.Run(() => _notes.GetAll());
            Notes.Clear();
            foreach (var n in data) Notes.Add(n);
            StatusText = $"{data.Count} not listeleniyor";
        }
        catch (Exception ex) 
        { 
            _logger.LogError("Notes load error", ex);
            StatusText = "Yükleme hatası."; 
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public void NewNote()
    {
        _editingId   = 0;
        EditTitle    = "";
        EditContent  = "";
        IsEditing    = true;
    }

    [RelayCommand]
    public void EditNote()
    {
        if (SelectedNote == null) return;
        _editingId   = SelectedNote.Id;
        EditTitle    = SelectedNote.Baslik;
        EditContent  = SelectedNote.Icerik;
        IsEditing    = true;
    }

    [RelayCommand]
    public async Task SaveNoteAsync()
    {
        if (string.IsNullOrWhiteSpace(EditTitle))
        {
            await _dialogService.ShowMessageAsync("Uyarı", "Not başlığı boş olamaz.");
            return;
        }

        try
        {
            if (_editingId == 0)
            {
                var n = new Not { Baslik = EditTitle.Trim(), Icerik = EditContent.Trim(), Tarih = DateTime.Now };
                await Task.Run(() => _notes.Add(n));
                StatusText = "Yeni not kaydedildi.";
            }
            else
            {
                var n = new Not { Id = _editingId, Baslik = EditTitle.Trim(), Icerik = EditContent.Trim(), Tarih = DateTime.Now };
                await Task.Run(() => _notes.Update(n));
                StatusText = "Not güncellendi.";
            }
            IsEditing = false;
            await LoadAsync();
        }
        catch (Exception ex) 
        { 
            _logger.LogError("Save note error", ex);
            await _dialogService.ShowMessageAsync("Hata", $"Kaydetme hatası: {ex.Message}");
        }
    }

    [RelayCommand]
    public void CancelEdit() => IsEditing = false;

    [RelayCommand]
    public async Task DeleteNoteAsync()
    {
        if (SelectedNote == null) return;

        bool confirm = await _dialogService.ShowConfirmAsync("Silme Onayı", "Bu notu silmek istediğinize emin misiniz?");
        if (!confirm) return;

        try
        {
            await Task.Run(() => _notes.Delete(SelectedNote.Id));
            await LoadAsync();
            StatusText = "Not silindi.";
        }
        catch (Exception ex) 
        { 
            _logger.LogError("Delete note error", ex);
            await _dialogService.ShowMessageAsync("Hata", $"Silme hatası: {ex.Message}");
        }
    }
}
