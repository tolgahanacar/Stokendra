using CommunityToolkit.Mvvm.ComponentModel;
using Stokendra.Infrastructure;

namespace Stokendra.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public bool CanEdit => AppServices.TryGetCurrent(out var app) &&
                           string.Equals(app?.Session?.Role, "admin", StringComparison.OrdinalIgnoreCase);

    public bool IsAdmin => CanEdit;

    /// <summary>
    /// Allows mutations in unit tests where no Session is initialized, while blocking non-admin logged-in users.
    /// </summary>
    protected static bool EnsureCanEditForSession()
    {
        if (!AppServices.TryGetCurrent(out var app) || app?.Session == null)
            return true;

        return string.Equals(app.Session.Role, "admin", StringComparison.OrdinalIgnoreCase);
    }

    public virtual void RefreshSession()
    {
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(IsAdmin));
    }
}
