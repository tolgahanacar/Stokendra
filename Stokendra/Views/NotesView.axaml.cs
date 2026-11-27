using Avalonia.Controls;
using Avalonia.Input;
using Stokendra.Models;
using Stokendra.ViewModels;
using System.Linq;

namespace Stokendra.Views;

public partial class NotesView : UserControl
{
    public NotesView()
    {
        InitializeComponent();

        Unloaded += (s, e) =>
        {
            if (DataContext is NotesViewModel vm)
            {
                vm.SelectedNotes.Clear();
                vm.SelectedNote = null;
            }
        };

        var listBox = this.FindControl<ListBox>("NotesGrid");
        if (listBox != null)
        {
            listBox.SelectionChanged += (s, e) =>
            {
                if (DataContext is NotesViewModel vm)
                {
                    if (listBox.SelectedItems != null)
                    {
                        vm.SelectedNotes.ReplaceAll(listBox.SelectedItems.OfType<Note>());
                    }
                    else
                    {
                        vm.SelectedNotes.Clear();
                    }
                    if (vm.SelectedNotes.Count == 1)
                    {
                        vm.SelectedNote = vm.SelectedNotes[0];
                    }
                    else if (vm.SelectedNotes.Count >= 2)
                    {
                        // Show the last clicked note in the preview panel
                        if (e.AddedItems.Count > 0 && e.AddedItems[e.AddedItems.Count - 1] is Note lastClicked)
                        {
                            vm.SelectedNote = lastClicked;
                        }
                        else if (vm.SelectedNote == null || !vm.SelectedNotes.Contains(vm.SelectedNote))
                        {
                            vm.SelectedNote = vm.SelectedNotes[^1];
                        }
                    }
                    else if (vm.SelectedNotes.Count == 0)
                    {
                        vm.SelectedNote = null;
                    }
                }
            };

            listBox.DoubleTapped += (s, e) =>
            {
                if (DataContext is NotesViewModel vm && vm.CanEdit)
                {
                    if (vm.EditNoteCommand.CanExecute(listBox.SelectedItems))
                    {
                        vm.EditNoteCommand.Execute(listBox.SelectedItems);
                    }
                }
            };
        }
    }
}
