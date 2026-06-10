using CommunityToolkit.Mvvm.ComponentModel;
using Stokendra.Infrastructure;

namespace Stokendra.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public bool CanEdit => AppServices.Current.Session?.Role == "admin";
    public bool IsAdmin => AppServices.Current.Session?.Role == "admin";

    public virtual void RefreshSession()
    {
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(IsAdmin));
    }
}
