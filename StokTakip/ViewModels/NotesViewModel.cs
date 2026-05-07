using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StokTakip.Data.Interfaces;
using StokTakip.Models;
using System.Collections.ObjectModel;

namespace StokTakip.ViewModels;

public partial class NotesViewModel : ViewModelBase
{
    private readonly INoteRepository _notes;

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private Not?   _selectedNote;

    // Yeni/düzenleme alanları
    [ObservableProperty] private string _editTitle   = "";
    [ObservableProperty] private string _editContent = "";
    [ObservableProperty] private bool   _isEditing;
    private int _editingId = 0;

    public ObservableCollection<Not> Notes { get; } = new();

    public NotesViewModel(INoteRepository notes)
    {
        _notes = notes;
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
            StatusText = $"{data.Count} not";
        }
        catch (Exception ex) { Console.WriteLine($"Notes load error: {ex.Message}"); }
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
        if (string.IsNullOrWhiteSpace(EditTitle)) return;
        try
        {
            if (_editingId == 0)
            {
                var n = new Not { Baslik = EditTitle.Trim(), Icerik = EditContent.Trim(), Tarih = DateTime.Now };
                await Task.Run(() => _notes.Add(n));
            }
            else
            {
                var n = new Not { Id = _editingId, Baslik = EditTitle.Trim(), Icerik = EditContent.Trim(), Tarih = DateTime.Now };
                await Task.Run(() => _notes.Update(n));
            }
            IsEditing = false;
            await LoadAsync();
        }
        catch (Exception ex) { Console.WriteLine($"Save note error: {ex.Message}"); }
    }

    [RelayCommand]
    public void CancelEdit() => IsEditing = false;

    [RelayCommand]
    public async Task DeleteNoteAsync()
    {
        if (SelectedNote == null) return;
        try
        {
            await Task.Run(() => _notes.Delete(SelectedNote.Id));
            await LoadAsync();
        }
        catch (Exception ex) { Console.WriteLine($"Delete note error: {ex.Message}"); }
    }
}
