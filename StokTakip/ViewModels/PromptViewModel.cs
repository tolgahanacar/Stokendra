using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace StokTakip.ViewModels;

public partial class PromptViewModel : ViewModelBase
{
    [ObservableProperty] private string _title;
    [ObservableProperty] private string _message;
    [ObservableProperty] private string _inputText;

    public PromptViewModel(string title, string message, string initialValue = "")
    {
        Title = title;
        Message = message;
        InputText = initialValue;
    }

    [RelayCommand]
    private void Ok() => CloseAction?.Invoke(true);

    [RelayCommand]
    private void Cancel() => CloseAction?.Invoke(false);

    public Action<bool>? CloseAction { get; set; }
}
