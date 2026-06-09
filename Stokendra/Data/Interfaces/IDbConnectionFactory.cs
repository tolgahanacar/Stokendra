using Microsoft.Data.Sqlite;

namespace Stokendra.Data.Interfaces;

/// <summary>
/// Veritabanı bağlantısı oluşturmak ve yönetmek için fabrika arayüzü.
/// SQLite'ın thread-safety ve connection pooling ayarlarını merkezileştirir.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Yeni, açık bir veritabanı bağlantısı döner.
    /// </summary>
    SqliteConnection CreateConnection();

    /// <summary>
    /// Bağlantı dizesini döner.
    /// </summary>
    string ConnectionString { get; }
}
