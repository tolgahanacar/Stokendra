using System.Threading;
using System.Threading.Tasks;

namespace Stokendra.Data.Interfaces;

/// <summary>
/// Repository interface for user authentication and password management.
/// </summary>
public interface IUserRepository
{
    Task<(bool success, string role)> VerifyPasswordAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the username and password asynchronously.
    /// </summary>
    Task<bool> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the user's password asynchronously.
    /// Verifies the old password and checks if the new password meets the security policy.
    /// </summary>
    /// <returns>True if the password was successfully changed.</returns>
    Task<bool> ChangePasswordAsync(string username, string oldPassword, string newPassword, CancellationToken cancellationToken = default);

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

    /// <summary>Gets all registered users.</summary>
    Task<System.Collections.Generic.List<Models.User>> GetUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a new user with hashed password and role.</summary>
    Task<bool> AddUserAsync(string username, string password, string role, CancellationToken cancellationToken = default);

    /// <summary>Deletes the user by ID.</summary>
    Task<bool> DeleteUserAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Updates the user's role by ID.</summary>
    Task<bool> UpdateUserRoleAsync(int id, string role, CancellationToken cancellationToken = default);
}
