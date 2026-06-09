using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.Data.Interfaces;

/// <summary>
/// Repository interface for user authentication and password management.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Verifies the username and password.
    /// If the password is in a legacy hash format, automatically upgrades it to the current format.
    /// </summary>
    /// <returns>True and the user role if the credentials are valid.</returns>
    Task<(bool success, string role)> VerifyPasswordAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the username and password synchronously.
    /// </summary>
    bool Authenticate(string username, string password);

    /// <summary>
    /// Changes the user's password.
    /// Verifies the old password and checks if the new password meets the security policy.
    /// </summary>
    /// <returns>True if the password was successfully changed.</returns>
    bool ChangePassword(string username, string oldPassword, string newPassword);

    /// <summary>
    /// Checks if the default "admin" password is still in use.
    /// </summary>
    bool IsDefaultAdminPasswordInUse();

    /// <summary>
    /// Validates the password against the password policy.
    /// </summary>
    /// <param name="password">The password to validate.</param>
    /// <param name="username">Optional username to ensure password doesn't contain it.</param>
    /// <returns>Error message if policy is violated, otherwise null.</returns>
    string? ValidatePasswordPolicy(string password, string? username = null);

    /// <summary>
    /// Resets the password after security code verification.
    /// </summary>
    Task<bool> ResetPasswordAsync(string username, string newPassword, CancellationToken cancellationToken = default);
}
