using System.Collections.Generic;
using Stokendra.Models;

namespace Stokendra.Data.Interfaces;

/// <summary>
/// Repository interface for application configurations and auxiliary data operations.
/// </summary>
public interface IConfigRepository
{
    /// <summary>Gets the configuration value for the specified key. Returns <paramref name="defaultValue"/> if not found.</summary>
    string GetConfig(string key, string defaultValue = "");

    /// <summary>Saves or updates the configuration value for the specified key.</summary>
    void SetConfig(string key, string value);

    /// <summary>Gets all measurement units sorted alphabetically.</summary>
    List<Unit> GetUnits();

    /// <summary>Creates a SQLite backup of the database to the specified destination path.</summary>
    void CreateBackup(string destinationPath);

    /// <summary>Writes a new record to the audit log.</summary>
    void WriteAuditLog(string action, string tableName, int recordId, string details);

    /// <summary>Clears all records in the audit log.</summary>
    void TruncateAuditLog();
}
