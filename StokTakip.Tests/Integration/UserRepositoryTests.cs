using StokTakip.Data.Interfaces;
using StokTakip.Tests.Helpers;

namespace StokTakip.Tests.Integration;

/// <summary>
/// <see cref="IUserRepository"/> implementasyonu için integration testler.
/// Kimlik doğrulama, şifre politikası ve şifre değiştirme senaryolarını kapsar.
/// </summary>
public class UserRepositoryTests : IDisposable
{
    private readonly TestDatabaseFactory _factory;
    private readonly IUserRepository _repo;

    public UserRepositoryTests()
    {
        _factory = new TestDatabaseFactory();
        _repo = _factory.Create();
    }

    public void Dispose() => _factory.Dispose();

    // ── Authenticate ──────────────────────────────────────────────────────

    [Fact]
    public void Authenticate_DefaultAdminCredentials_ReturnsTrue()
    {
        Assert.True(_repo.Authenticate("admin", "admin"));
    }

    [Fact]
    public void Authenticate_WrongPassword_ReturnsFalse()
    {
        Assert.False(_repo.Authenticate("admin", "yanlis_sifre"));
    }

    [Fact]
    public void Authenticate_NonExistingUser_ReturnsFalse()
    {
        Assert.False(_repo.Authenticate("yokkullanici", "herhangi"));
    }

    [Fact]
    public void Authenticate_EmptyCredentials_ReturnsFalse()
    {
        Assert.False(_repo.Authenticate("", ""));
        Assert.False(_repo.Authenticate("admin", ""));
        Assert.False(_repo.Authenticate("", "admin"));
    }

    // ── IsDefaultAdminPasswordInUse ───────────────────────────────────────

    [Fact]
    public void IsDefaultAdminPasswordInUse_FreshDatabase_ReturnsTrue()
    {
        Assert.True(_repo.IsDefaultAdminPasswordInUse());
    }

    [Fact]
    public void IsDefaultAdminPasswordInUse_AfterPasswordChange_ReturnsFalse()
    {
        _repo.ChangePassword("admin", "admin", "Yeni@Sifre123!");

        Assert.False(_repo.IsDefaultAdminPasswordInUse());
    }

    // ── ValidatePasswordPolicy ────────────────────────────────────────────

    [Fact]
    public void ValidatePasswordPolicy_ValidPassword_ReturnsNull()
    {
        var error = _repo.ValidatePasswordPolicy("Guclu@Sifre123");
        Assert.Null(error);
    }

    [Fact]
    public void ValidatePasswordPolicy_TooShort_ReturnsError()
    {
        var error = _repo.ValidatePasswordPolicy("Abc1!");
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidatePasswordPolicy_EmptyPassword_ReturnsError()
    {
        var error = _repo.ValidatePasswordPolicy("");
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidatePasswordPolicy_ContainsWhitespace_ReturnsError()
    {
        var error = _repo.ValidatePasswordPolicy("Abc 123!@#");
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidatePasswordPolicy_ContainsUsername_ReturnsError()
    {
        var error = _repo.ValidatePasswordPolicy("Admin@123456", "admin");
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("onlylowercase!!")]   // sadece küçük harf + özel karakter = 2 kategori
    [InlineData("ONLYUPPERCASE!!")]   // sadece büyük harf + özel karakter = 2 kategori
    [InlineData("1234567890!!")]      // sadece rakam + özel karakter = 2 kategori
    public void ValidatePasswordPolicy_InsufficientComplexity_ReturnsError(string password)
    {
        // Politika: en az 3 farklı karakter kategorisi (küçük, büyük, rakam, özel)
        var error = _repo.ValidatePasswordPolicy(password);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("alllowercase1!")]   // küçük + rakam + özel = 3 kategori → geçer
    [InlineData("ALLUPPERCASE1!")]   // büyük + rakam + özel = 3 kategori → geçer
    [InlineData("NoSpecialChar123")] // küçük + büyük + rakam = 3 kategori → geçer
    [InlineData("NoDigit!@#AbcDef")] // küçük + büyük + özel = 3 kategori → geçer
    public void ValidatePasswordPolicy_ThreeCategoryPassword_ReturnsNull(string password)
    {
        // Politika 3/4 kategori gerektiriyor, bu şifreler 3 kategoriye sahip
        var error = _repo.ValidatePasswordPolicy(password);
        Assert.Null(error);
    }

    // ── ChangePassword ────────────────────────────────────────────────────

    [Fact]
    public void ChangePassword_ValidOldPassword_ReturnsTrue()
    {
        var result = _repo.ChangePassword("admin", "admin", "Yeni@Sifre123!");
        Assert.True(result);
    }

    [Fact]
    public void ChangePassword_AfterChange_NewPasswordWorks()
    {
        _repo.ChangePassword("admin", "admin", "Yeni@Sifre123!");

        Assert.True(_repo.Authenticate("admin", "Yeni@Sifre123!"));
        Assert.False(_repo.Authenticate("admin", "admin"));
    }

    [Fact]
    public void ChangePassword_WrongOldPassword_ReturnsFalse()
    {
        var result = _repo.ChangePassword("admin", "yanlis", "Yeni@Sifre123!");
        Assert.False(result);
    }

    [Fact]
    public void ChangePassword_SameAsOldPassword_ReturnsFalse()
    {
        // Aynı şifreye değiştirme engellenmeli
        var result = _repo.ChangePassword("admin", "admin", "admin");
        Assert.False(result);
    }

    [Fact]
    public void ChangePassword_WeakNewPassword_ReturnsFalse()
    {
        var result = _repo.ChangePassword("admin", "admin", "zayif");
        Assert.False(result);
    }
}
