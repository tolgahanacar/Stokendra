using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokendra.Infrastructure;
using System;

namespace Stokendra.ViewModels;

public partial class PromptViewModel : ViewModelBase
{
    [ObservableProperty] private string _title;
    [ObservableProperty] private string _message;
    [ObservableProperty] private string _inputText;
    [ObservableProperty] private char _passwordChar = '\0';
    [ObservableProperty] private string _watermark = LocalizationManager.L("prompt_default_watermark");

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
