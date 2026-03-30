using System.Reflection;
using Microsoft.Data.Sqlite;

namespace CMSFeeApp.Data;

public class MigrationRunner
{
    private readonly DatabaseContext _context;

    public MigrationRunner(DatabaseContext context)
    {
        _context = context;
    }

    public void RunMigrations()
    {
        var connection = _context.GetConnection();
        EnsureMigrationsTable(connection);

        var scripts = GetMigrationScripts();
        foreach (var (id, sql) in scripts)
        {
            if (!IsMigrationApplied(connection, id))
            {
                ApplyMigration(connection, id, sql);
            }
        }
    }

    private static void EnsureMigrationsTable(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS migrations (
                id TEXT NOT NULL PRIMARY KEY,
                applied_at TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static bool IsMigrationApplied(SqliteConnection connection, string id)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM migrations WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        var result = cmd.ExecuteScalar();
        return Convert.ToInt64(result) > 0;
    }

    private static void ApplyMigration(SqliteConnection connection, string id, string sql)
    {
        using var transaction = connection.BeginTransaction();
        try
        {
            using var scriptCmd = connection.CreateCommand();
            scriptCmd.Transaction = transaction;
            scriptCmd.CommandText = sql;
            scriptCmd.ExecuteNonQuery();

            using var logCmd = connection.CreateCommand();
            logCmd.Transaction = transaction;
            logCmd.CommandText = "INSERT INTO migrations (id, applied_at) VALUES (@id, @applied_at)";
            logCmd.Parameters.AddWithValue("@id", id);
            logCmd.Parameters.AddWithValue("@applied_at", DateTime.UtcNow.ToString("O"));
            logCmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static IEnumerable<(string id, string sql)> GetMigrationScripts()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n);

        foreach (var resourceName in resourceNames)
        {
            // Resource name is e.g. "CMSFeeApp.Data.Migrations.001_initial_schema.sql"
            // Extract the second-to-last dot-segment as the migration id: "001_initial_schema"
            var parts = resourceName.Split('.');
            var id = parts.Length >= 2 ? parts[^2] : resourceName;

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            var sql = reader.ReadToEnd();
            yield return (id, sql);
        }
    }
}
