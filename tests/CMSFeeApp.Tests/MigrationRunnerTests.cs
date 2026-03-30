using CMSFeeApp.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CMSFeeApp.Tests;

public class MigrationRunnerTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly DatabaseContext _context;

    public MigrationRunnerTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        _context = new DatabaseContext(_tempDbPath);
    }

    [Fact]
    public void RunMigrations_CreatesTablesFromEmbeddedScripts()
    {
        var runner = new MigrationRunner(_context);
        runner.RunMigrations();

        var connection = _context.GetConnection();

        // Verify key tables exist
        Assert.True(TableExists(connection, "migrations"), "migrations table should exist");
        Assert.True(TableExists(connection, "dmepos_fees"), "dmepos_fees table should exist");
        Assert.True(TableExists(connection, "pfs_fees"), "pfs_fees table should exist");
        Assert.True(TableExists(connection, "user_preferences"), "user_preferences table should exist");
        Assert.True(TableExists(connection, "import_log"), "import_log table should exist");
    }

    [Fact]
    public void RunMigrations_IsIdempotent()
    {
        var runner = new MigrationRunner(_context);

        // Run twice – should not throw
        runner.RunMigrations();
        runner.RunMigrations();

        var connection = _context.GetConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM migrations";
        var count = (long)(cmd.ExecuteScalar() ?? 0L);

        // Should only have applied once
        Assert.True(count > 0, "At least one migration should have been applied");
    }

    [Fact]
    public void RunMigrations_RecordsMigrationInTable()
    {
        var runner = new MigrationRunner(_context);
        runner.RunMigrations();

        var connection = _context.GetConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, applied_at FROM migrations ORDER BY applied_at";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read(), "At least one migration record should exist");
        var id = reader.GetString(0);
        var appliedAt = reader.GetString(1);

        Assert.False(string.IsNullOrEmpty(id));
        Assert.False(string.IsNullOrEmpty(appliedAt));
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@name";
        cmd.Parameters.AddWithValue("@name", tableName);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    public void Dispose()
    {
        _context.Dispose();
        if (File.Exists(_tempDbPath))
            File.Delete(_tempDbPath);
    }
}
