using CMSFeeApp.Core;
using CMSFeeApp.Core.Models;
using Microsoft.Data.Sqlite;

namespace CMSFeeApp.Data.Repositories;

public class ImportLogRepository
{
    private readonly DatabaseContext _context;

    public ImportLogRepository(DatabaseContext context)
    {
        _context = context;
    }

    public void LogImport(FeeScheduleType scheduleType, int year, string? filePath, int recordsImported, bool success, string? errorMessage = null)
    {
        var connection = _context.GetConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO import_log (schedule_type, year, file_path, records_imported, imported_at, success, error_message)
            VALUES (@schedule_type, @year, @file_path, @records_imported, @imported_at, @success, @error_message)
            """;
        cmd.Parameters.AddWithValue("@schedule_type", scheduleType.ToString());
        cmd.Parameters.AddWithValue("@year", year);
        cmd.Parameters.AddWithValue("@file_path", (object?)filePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@records_imported", recordsImported);
        cmd.Parameters.AddWithValue("@imported_at", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@success", success ? 1 : 0);
        cmd.Parameters.AddWithValue("@error_message", (object?)errorMessage ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<ImportLogEntry> GetRecentLogs(int count = 50)
    {
        var connection = _context.GetConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, schedule_type, year, file_path, records_imported, imported_at, success, error_message
            FROM import_log
            ORDER BY id DESC
            LIMIT @count
            """;
        cmd.Parameters.AddWithValue("@count", count);

        var results = new List<ImportLogEntry>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new ImportLogEntry
            {
                Id = reader.GetInt32(0),
                ScheduleType = reader.GetString(1),
                Year = reader.GetInt32(2),
                FilePath = reader.IsDBNull(3) ? null : reader.GetString(3),
                RecordsImported = reader.GetInt32(4),
                ImportedAt = DateTime.Parse(reader.GetString(5)),
                Success = reader.GetInt32(6) != 0,
                ErrorMessage = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }
        return results;
    }
}
