using Microsoft.Data.Sqlite;

namespace CMSFeeApp.Data;

public class DatabaseContext : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public DatabaseContext(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _connectionString = $"Data Source={databasePath}";
    }

    public SqliteConnection GetConnection()
    {
        if (_connection is null || _connection.State != System.Data.ConnectionState.Open)
        {
            _connection?.Dispose();
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();

            // Enable WAL mode for better concurrent performance
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
            cmd.ExecuteNonQuery();
        }
        return _connection;
    }

    public static string GetDefaultDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "CMSFeeApp", "data", "cms_fees.db");
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
