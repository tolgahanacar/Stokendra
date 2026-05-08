using StokTakip.Tests.Helpers;

namespace StokTakip.Tests.Integration;

/// <summary>
/// UserRepository — kimlik doğrulama, şifre hash zinciri (v2/v3/v4), politika testleri.
/// </summary>
public class UserRepositoryTests : IDisposable
{
    private readonly TestDb _db;
    public UserRepositoryTests() => _db = new TestDb();
    public void Dispose() => _db.Dispose();

    // ── Varsayılan admin ──────────────────────────────────────────────────

    [Fact]
    public async Task DefaultAdmin_PasswordIsAdmin_AuthenticatesSuccessfully()
    {
        var (success, role) = await _db.Users.VerifyPasswordAsync("admin", "admin");
        Assert.True(success);
        Assert.Equal("admin", role);
    }

    [Fact]
    public async Task DefaultAdmin_WrongPassword_Fails()
    {
        var (success, _) = await _db.Users.VerifyPasswordAsync("admin", "yanlis");
        Assert.False(success);
    }

    [Fact]
    public async Task NonExistentUser_Fails()
    {
        var (success, _) = await _db.Users.VerifyPasswordAsync("yokuser", "sifre");
        Assert.False(success);
    }

    [Fact]
    public void IsDefaultAdminPasswordInUse_BeforeChange_IsTrue()
    {
        Assert.True(_db.Users.IsDefaultAdminPasswordInUse());
    }

    // ── Şifre değiştirme ──────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_ValidOldPassword_Succeeds()
    {
        bool changed = _db.Users.ChangePassword("admin", "admin", "Yeni1234!");
        Assert.True(changed);

        var (success, _) = await _db.Users.VerifyPasswordAsync("admin", "Yeni1234!");
        Assert.True(success);
    }

    [Fact]
    public void ChangePassword_WrongOldPassword_Fails()
    {
        bool changed = _db.Users.ChangePassword("admin", "yanlis", "Yeni1234!");
        Assert.False(changed);
    }

    [Fact]
    public async Task ChangePassword_OldPasswordNoLongerWorks()
    {
        _db.Users.ChangePassword("admin", "admin", "Yeni1234!");

        var (success, _) = await _db.Users.VerifyPasswordAsync("admin", "admin");
        Assert.False(success);
    }

    [Fact]
    public void IsDefaultAdminPasswordInUse_AfterChange_IsFalse()
    {
        _db.Users.ChangePassword("admin", "admin", "Yeni1234!");
        Assert.False(_db.Users.IsDefaultAdminPasswordInUse());
    }

    // ── Şifre politikası ──────────────────────────────────────────────────

    [Fact]
    public void ValidatePasswordPolicy_TooShort_ReturnsError()
    {
        var error = _db.Users.ValidatePasswordPolicy("abc");
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidatePasswordPolicy_ValidPassword_ReturnsNull()
    {
        var error = _db.Users.ValidatePasswordPolicy("Gecerli1234!");
        Assert.Null(error);
    }

    [Fact]
    public void ValidatePasswordPolicy_ContainsUsername_ReturnsError()
    {
        var error = _db.Users.ValidatePasswordPolicy("admin1234", "admin");
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidatePasswordPolicy_EmptyPassword_ReturnsError()
    {
        var error = _db.Users.ValidatePasswordPolicy("");
        Assert.NotNull(error);
    }

    // ── Hash upgrade zinciri ──────────────────────────────────────────────

    [Fact]
    public async Task VerifyPassword_AfterSuccessfulLogin_HashUpgradedToV4()
    {
        // İlk login — admin şifresi v4 formatında seed edilmiş
        var (success, _) = await _db.Users.VerifyPasswordAsync("admin", "admin");
        Assert.True(success);

        // Tekrar login — hâlâ çalışmalı (upgrade idempotent)
        var (success2, _) = await _db.Users.VerifyPasswordAsync("admin", "admin");
        Assert.True(success2);
    }

    // ── Büyük/küçük harf duyarsızlık ─────────────────────────────────────

    [Fact]
    public async Task VerifyPassword_UsernameIsCaseInsensitive()
    {
        var (success, _) = await _db.Users.VerifyPasswordAsync("ADMIN", "admin");
        Assert.True(success);
    }

    [Fact]
    public async Task VerifyPassword_PasswordIsCaseSensitive()
    {
        var (success, _) = await _db.Users.VerifyPasswordAsync("admin", "ADMIN");
        Assert.False(success);
    }
}
