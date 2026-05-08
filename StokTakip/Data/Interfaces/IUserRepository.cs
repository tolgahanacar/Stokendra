namespace StokTakip.Data.Interfaces;

/// <summary>
/// Kullanıcı kimlik doğrulama ve şifre yönetimi için repository arayüzü.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Kullanıcı adı ve şifreyi doğrular.
    /// Şifre eski hash formatındaysa otomatik olarak güncel formata yükseltir.
    /// </summary>
    /// <returns>Kimlik bilgileri geçerliyse <c>true</c> ve kullanıcı rolünü döndürür.</returns>
    Task<(bool success, string role)> VerifyPasswordAsync(string username, string password);

    bool Authenticate(string username, string password);

    /// <summary>
    /// Kullanıcının şifresini değiştirir.
    /// Eski şifre doğrulanır ve yeni şifre politikasına uygunluk kontrol edilir.
    /// </summary>
    /// <returns>Şifre başarıyla değiştirildiyse <c>true</c>.</returns>
    bool ChangePassword(string username, string oldPassword, string newPassword);

    /// <summary>
    /// Varsayılan "admin" şifresinin hâlâ kullanımda olup olmadığını kontrol eder.
    /// </summary>
    bool IsDefaultAdminPasswordInUse();

    /// <summary>
    /// Verilen şifrenin politikaya uygunluğunu kontrol eder.
    /// </summary>
    /// <param name="password">Kontrol edilecek şifre.</param>
    /// <param name="username">Şifrenin kullanıcı adını içermemesi için opsiyonel kullanıcı adı.</param>
    /// <returns>Politika ihlali varsa hata mesajı, uygunsa <c>null</c>.</returns>
    string? ValidatePasswordPolicy(string password, string? username = null);

    /// <summary>
    /// Güvenlik kodu doğrulaması sonrası şifreyi sıfırlar.
    /// </summary>
    Task<bool> ResetPasswordAsync(string username, string newPassword);
}
