namespace Stokendra.Infrastructure;

/// <summary>
/// Immutable oturum bağlamı. Login başarısında oluşturulur, public setter yoktur.
/// Oturum bilgisine erişmek için <see cref="AppServices.Session"/> kullanın.
/// </summary>
public sealed class UserSession
{
    /// <summary>Oturum açan kullanıcı adı.</summary>
    public string Username { get; }

    /// <summary>Kullanıcı rolü (örn. "admin").</summary>
    public string Role { get; }

    /// <summary>Oturum açılış zamanı.</summary>
    public DateTime LoginTime { get; }

    private UserSession(string username, string role)
    {
        Username = username;
        Role = role;
        LoginTime = DateTime.Now;
    }

    /// <summary>
    /// Yeni bir oturum oluşturur. Yalnızca başarılı kimlik doğrulama sonrasında çağrılmalıdır.
    /// </summary>
    public static UserSession Create(string username, string role = "admin")
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Kullanıcı adı boş olamaz.", nameof(username));
        return new UserSession(username.Trim(), role.Trim());
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Username} ({Role}) @ {LoginTime:HH:mm:ss}";
}
