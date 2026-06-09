using Avalonia.Markup.Xaml;
using System;

namespace Stokendra.Infrastructure;

public class LExtension : MarkupExtension
{
    public string Key { get; set; } = "";

    public LExtension() { }
    public LExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return LocalizationManager.L(Key);
    }
}
